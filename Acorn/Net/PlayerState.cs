using System.Net.Sockets;
using Acorn.Database.Models;
using Acorn.Extensions;
using Acorn.Infrastructure.Communicators;
using Acorn.Net.Models;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Warp;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net;

public class PlayerState : IDisposable
{
    private readonly Action<PlayerState> _onDispose;
    private readonly PacketResolver _resolver = new("Moffat.EndlessOnline.SDK.Protocol.Net.Client");
    private readonly IServiceProvider _serviceProvider;
    private readonly PingSequenceStart _upcomingSequence;
    private readonly ILogger<PlayerState> _logger;
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly CancellationToken _cancellationToken;

    public PlayerState(
        IServiceProvider services,
        ICommunicator communicator,
        ILogger<PlayerState> logger,
        int sessionId,
        Action<PlayerState> onDispose
    )
    {
        _logger = logger;
        _cancellationToken = _tokenSource.Token;
        _upcomingSequence = PingSequenceStart.Generate(Rnd);
        _logger.LogInformation("New client connected from {Location}", communicator.GetConnectionOrigin());
        _onDispose = onDispose;
        _serviceProvider = services;
        SessionId = sessionId;
        StartSequence = InitSequenceStart.Generate(Rnd);
        Communicator = communicator;
        Task.Run(Listen);
    }

    public Random Rnd { get; } = new();

    public ClientState ClientState { get; set; } = ClientState.Uninitialized;
    public bool NeedPong { get; set; } = false;
    public int ClientEncryptionMulti { get; set; } = 0;
    public int ServerEncryptionMulti { get; set; } = 0;
    public PacketSequencer PacketSequencer { get; set; } = new(ZeroSequence.Instance);
    public InitSequenceStart StartSequence { get; set; }
    public ICommunicator Communicator { get; }
    public Account? Account { get; set; }
    public bool IsListeningToGlobal { get; set; }

    public int SessionId { get; set; }
    public WarpSession? WarpSession { get; set; }

    public Character? Character { get; set; }

    public MapState? CurrentMap { get; set; }

    public void Dispose()
    {
        _onDispose(this);
        Communicator.Close();
    }

    public async Task Listen()
    {
        while (_cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                var stream = Communicator.Receive();

                var len1 = (byte)stream.ReadByte();
                var len2 = (byte)stream.ReadByte();

                var decodedLength = NumberEncoder.DecodeNumber([len1, len2]);
                _logger.LogDebug("Len1 {Len1}, Len2 {Len2}, Decoded length {DecodedLength}", len1, len2, decodedLength);
                var bytes = new byte[decodedLength];
                await stream.ReadExactlyAsync(bytes.AsMemory(0, decodedLength), _cancellationToken);

                var decodedBytes = ClientEncryptionMulti switch
                {
                    0 => bytes,
                    _ => DataEncrypter.SwapMultiples(DataEncrypter.Deinterleave(DataEncrypter.FlipMSB(bytes)),
                        ClientEncryptionMulti)
                };

                var reader = new EoReader(decodedBytes);
                var action = (PacketAction)reader.GetByte();
                var family = (PacketFamily)reader.GetByte();

                HandleSequence(family, action, ref reader);

                var dataReader = reader.Slice();

                var packet = _resolver.Create(family, action);
                packet.Deserialize(dataReader);
                _logger.LogDebug("[Client] {Packet}", packet.ToString());

                var handlerType = typeof(IPacketHandler<>).MakeGenericType(packet.GetType());
                if (_serviceProvider.GetService(handlerType) is not IHandler handler)
                {
                    _logger.LogError("Handler not registered for packet of type {PacketType} Exiting...", packet.GetType());
                    break;
                }

                await handler.HandleAsync(this, packet);
            }
            catch (Exception e)
            {
                _logger.LogError("Caught exception \"{Message}\" terminating...", e.Message);
                break;
            }
        }
        Disconnect();
        Dispose();
    }

    private void HandleSequence(PacketFamily family, PacketAction action, ref EoReader reader)
    {
        if (family == PacketFamily.Init && action == PacketAction.Init)
        {
            PacketSequencer.NextSequence();
            return;
        }

        if (family == PacketFamily.Connection && action == PacketAction.Ping)
        {
            PacketSequencer = PacketSequencer.WithSequenceStart(_upcomingSequence);
        }

        var serverSequence = PacketSequencer.NextSequence();
        var clientSequence = serverSequence switch
        {
            >= (int)EoNumericLimits.CHAR_MAX => reader.GetShort(),
            _ => reader.GetChar()
        };

        if (serverSequence != clientSequence)
        {
            _logger.LogError("Expected sequence {Expected} got {Actual}", serverSequence, clientSequence);
        }
    }

    public async Task Send(IPacket packet)
    {
        _logger.LogDebug("[Server] {Packet}", packet.ToString());
        var writer = new EoWriter();
        writer.AddByte((int)packet.Action);
        writer.AddByte((int)packet.Family);
        packet.Serialize(writer);
        var bytes = packet switch
        {
            InitInitServerPacket _ => writer.ToByteArray(),
            _ => DataEncrypter.FlipMSB(DataEncrypter.Interleave(DataEncrypter.SwapMultiples(writer.ToByteArray(), ServerEncryptionMulti)))
        };

        var encodedLength = NumberEncoder.EncodeNumber(bytes.Length);
        var fullBytes = encodedLength[..2].Concat(bytes);
        await Communicator.Send(fullBytes);
    }

    public Task ServerMessage(string message)
        => Send(new TalkServerServerPacket
        {
            Message = message
        });

    public Task Refresh()
        => Character switch
        {
            null => throw new InvalidOperationException("Cannot refresh player where the selected character is not initialised"),
            _ => CurrentMap switch
            {
                null => throw new InvalidOperationException("Cannot refresh player where the selected character's map is not initialised"),
                _ => Warp(CurrentMap, Character.X, Character.Y)
            }
        };

    public async Task Warp(MapState targetMap, int x, int y, WarpEffect warpEffect = WarpEffect.None)
    {
        WarpSession = new WarpSession(x, y, this, targetMap, warpEffect);

        if (WarpSession.IsLocal is false)
        {
            if (CurrentMap is not null)
            {
                await CurrentMap.NotifyLeave(this, warpEffect);
            }

            await targetMap.NotifyEnter(this, warpEffect);
        }
        
        await WarpSession.Execute();
    }

    public void Disconnect()
    {
        _tokenSource.Cancel();
    }
}