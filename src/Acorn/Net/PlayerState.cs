using Acorn.Database.Models;
using Acorn.Infrastructure.Communicators;
using Acorn.Net.Models;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Warp;
using Acorn.Options;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Character = Acorn.Game.Models.Character;

namespace Acorn.Net;

public class PlayerState : IDisposable
{
    private readonly CancellationToken _cancellationToken;
    private readonly IEnumerable<IPacketHandler> _handlers;
    private readonly ILogger<PlayerState> _logger;
    private readonly Action<PlayerState> _onDispose;
    private readonly PacketLog _packetLog = new();
    private readonly PacketResolver _resolver = new("Moffat.EndlessOnline.SDK.Protocol.Net.Client");
    private readonly ServerOptions _serverOptions;
    private readonly CancellationTokenSource _tokenSource = new();
    private string? _disconnectReason;
    private PingSequenceStart _upcomingSequence;

    public PlayerState(
        IEnumerable<IPacketHandler> handlers,
        ICommunicator communicator,
        ILogger<PlayerState> logger,
        IOptions<ServerOptions> serverOptions,
        int sessionId,
        Action<PlayerState> onDispose
    )
    {
        _logger = logger;
        _serverOptions = serverOptions.Value;
        _cancellationToken = _tokenSource.Token;
        _upcomingSequence = PingSequenceStart.Generate(Rnd);
        _logger.LogInformation("New client connected from {Location}", communicator.GetConnectionOrigin());
        _onDispose = onDispose;
        _handlers = handlers;
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

    // Spell casting state
    public int Timestamp { get; set; }
    public int? SpellId { get; set; }

    public void Dispose()
    {
        if (_disconnectReason != null)
        {
            _logger.LogInformation("Player disconnected: {Reason}", _disconnectReason);
        }

        _onDispose(this);
        // Fire and forget close - we're already in synchronous Dispose
        _ = Communicator.CloseAsync(_cancellationToken);
    }

    /// <summary>
    ///     Updates the upcoming ping sequence. This should be called before sending a CONNECTION_PLAYER ping.
    ///     The sequencer will be updated when the client responds with CONNECTION_PING.
    /// </summary>
    public void SetUpcomingPingSequence(PingSequenceStart newSequence)
    {
        _upcomingSequence = newSequence;
    }

    public async Task Listen()
    {
        while (_cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                if (!Communicator.IsConnected)
                {
                    CloseWithReason("Connection closed by client");
                    break;
                }

                var stream = Communicator.Receive();

                var len1 = (byte)stream.ReadByte();
                var len2 = (byte)stream.ReadByte();

                var decodedLength = NumberEncoder.DecodeNumber([len1, len2]);
                if (_serverOptions.LogPackets)
                {
                    _logger.LogDebug("Len1 {Len1}, Len2 {Len2}, Decoded length {DecodedLength}", len1, len2,
                        decodedLength);
                }

                if (decodedLength <= 0 || decodedLength > 65535)
                {
                    CloseWithReason($"Invalid packet length: {decodedLength}");
                    break;
                }

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

                // Handle sequence before rate limiting to keep client and server in sync
                var serverSequence = HandleSequence(family, action, ref reader);

                // Rate limiting check
                if (_packetLog.ShouldRateLimit(action, family))
                {
                    _logger.LogDebug("Rate limited: {Action}_{Family}", action, family);
                    // Send rate-limit response packet: 0xfe 0xfe <sequence>
                    var rateLimitResponse = new byte[3];
                    rateLimitResponse[0] = 0xfe;
                    rateLimitResponse[1] = 0xfe;

                    // Encode the server sequence as a single byte or short depending on value
                    if (serverSequence < 256)
                    {
                        rateLimitResponse[2] = (byte)serverSequence;
                    }
                    else
                    {
                        // If sequence is >= 256, we need to handle it differently
                        rateLimitResponse[2] = (byte)(serverSequence & 0xFF);
                    }

                    var encodedLength = NumberEncoder.EncodeNumber(rateLimitResponse.Length);
                    var fullBytes = encodedLength[..2].Concat(rateLimitResponse);
                    await Communicator.Send(fullBytes);
                    continue;
                }

                var dataReader = reader.Slice();

                var packet = _resolver.Create(family, action);
                packet.Deserialize(dataReader);
                if (_serverOptions.LogPackets)
                {
                    _logger.LogDebug("[Client] {Packet}", packet.ToString());
                }

                var handlerType = typeof(IPacketHandler<>).MakeGenericType(packet.GetType());
                var resolvedHandler = _handlers.FirstOrDefault(h => handlerType.IsInstanceOfType(h));
                if (resolvedHandler is null)
                {
                    _logger.LogError("Handler not registered for packet of type {PacketType} Skipping...",
                        packet.GetType());
                    continue;
                }

                // Record packet for rate limiting after successful processing
                _packetLog.RecordPacket(action, family);

                await resolvedHandler.HandleAsync(this, packet);
            }
            catch (EndOfStreamException)
            {
                CloseWithReason("Connection closed");
                break;
            }
            catch (IOException ex)
            {
                CloseWithReason($"I/O error: {ex.Message}");
                break;
            }
            catch (OperationCanceledException)
            {
                CloseWithReason("Operation cancelled");
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Caught exception terminating...");
                CloseWithReason($"Unhandled error: {e.Message}");
                break;
            }
        }

        Disconnect();
        Dispose();
    }

    private int HandleSequence(PacketFamily family, PacketAction action, ref EoReader reader)
    {
        // Init.Init packets have no sequence at all
        if (family == PacketFamily.Init && action == PacketAction.Init)
        {
            PacketSequencer.NextSequence();
            return 0;
        }

        // Other Init packets do have sequence bytes - always consume them
        if (family == PacketFamily.Init)
        {
            _ = reader.GetChar(); // Consume sequence byte
            var seq = PacketSequencer.NextSequence();
            return seq;
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

        if (_serverOptions.EnforceSequence && serverSequence != clientSequence)
        {
            var message = $"Sending invalid sequence: Got {clientSequence}, expected {serverSequence}";
            _logger.LogWarning(message);
            CloseWithReason(message);
            throw new InvalidOperationException(message);
        }

        return serverSequence;
    }

    private void CloseWithReason(string reason)
    {
        _disconnectReason = reason;
        _logger.LogInformation("Closing connection: {Reason}", reason);
    }

    public async Task Send(IPacket packet)
    {
        if (_serverOptions.LogPackets)
        {
            _logger.LogDebug("[Server] {Packet}", packet.ToString());
        }

        var writer = new EoWriter();
        writer.AddByte((int)packet.Action);
        writer.AddByte((int)packet.Family);
        packet.Serialize(writer);
        var bytes = packet switch
        {
            InitInitServerPacket _ => writer.ToByteArray(),
            _ => DataEncrypter.FlipMSB(
                DataEncrypter.Interleave(DataEncrypter.SwapMultiples(writer.ToByteArray(), ServerEncryptionMulti)))
        };

        var encodedLength = NumberEncoder.EncodeNumber(bytes.Length);
        var fullBytes = encodedLength[..2].Concat(bytes);
        await Communicator.Send(fullBytes);
    }

    public void Disconnect()
    {
        _tokenSource.Cancel();
    }
}