using System.Collections.Concurrent;
using System.Reflection;
using Acorn.Database.Models;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
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
    // Static cache for handler method invocation - avoids repeated reflection
    private static readonly ConcurrentDictionary<Type, Func<object, PlayerState, IPacket, Task>> _handlerInvokeCache = new();
    
    // Static cache for [RequiresCharacter] attribute check per handler type
    private static readonly ConcurrentDictionary<Type, bool> _requiresCharacterCache = new();
    
    private readonly CancellationToken _cancellationToken;
    private readonly IEnumerable<IPacketHandler> _handlers;
    private readonly ILogger<PlayerState> _logger;
    private readonly AcornMetrics _metrics;
    private readonly Func<PlayerState, Task> _onDispose;
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
        AcornMetrics metrics,
        int sessionId,
        Func<PlayerState, Task> onDispose
    )
    {
        _logger = logger;
        _metrics = metrics;
        _serverOptions = serverOptions.Value;
        _cancellationToken = _tokenSource.Token;
        _upcomingSequence = ConstrainedSequence.GeneratePingStart(Rnd);
        _logger.PlayerConnected(sessionId, communicator.GetConnectionOrigin());
        _metrics.ConnectionsTotal.Add(1);
        _metrics.PlayersOnline.Add(1);
        _onDispose = onDispose;
        _handlers = handlers;
        SessionId = sessionId;
        StartSequence = ConstrainedSequence.GenerateInitStart(Rnd);
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

    // Character deletion state
    public int? CharacterIdToDelete { get; set; }

    // NPC interaction state (for shops, banks, etc.)
    public int? InteractingNpcIndex { get; set; }

    // Chest interaction state
    public Coords? InteractingChestCoords { get; set; }

    // Trade state
    public TradeSession? TradeSession { get; set; }
    
    // Pending trade request - the player who has requested to trade with us
    public int? PendingTradeRequestFromPlayerId { get; set; }

    // Admin state
    public bool IsFrozen { get; set; }
    public bool IsMuted { get; set; }
    public bool IsJailed { get; set; }

    // Board interaction state
    public int? InteractingBoardId { get; set; }

    // Guild interaction state - the player who has requested to join/trade with us
    public int? InteractingPlayerId { get; set; }

    // Inn/Sleep state
    public int? SleepCost { get; set; }

    public void Dispose()
    {
        _logger.PlayerDisconnected(SessionId, Account?.Username, Character?.Name, _disconnectReason ?? "unknown");
        _metrics.DisconnectionsTotal.Add(1);
        _metrics.PlayersOnline.Add(-1);

        _ = Communicator.CloseAsync(CancellationToken.None);
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

                var lenBuf = new byte[2];
                await stream.ReadExactlyAsync(lenBuf, _cancellationToken);
                var len1 = lenBuf[0];
                var len2 = lenBuf[1];

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
                    _logger.PacketRateLimited(action, family, SessionId);
                    _metrics.PacketsRateLimited.Add(1);
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
                    _metrics.PacketsUnhandled.Add(1);
                    continue;
                }

                // Record packet for rate limiting after successful processing
                _packetLog.RecordPacket(action, family);

                // Pipeline check: reject packets requiring a character if player hasn't loaded one
                var requiresCharacter = _requiresCharacterCache.GetOrAdd(
                    resolvedHandler.GetType(),
                    type => type.GetCustomAttribute<RequiresCharacterAttribute>() != null);

                if (requiresCharacter && (Character is null || CurrentMap is null))
                {
                    _logger.LogWarning(
                        "Player {SessionId} attempted {PacketType} without character or map",
                        SessionId, packet.GetType().Name);
                    continue;
                }

                // Use cached reflection to invoke the typed HandleAsync method
                var invoker = _handlerInvokeCache.GetOrAdd(packet.GetType(), packetType =>
                {
                    var method = typeof(IPacketHandler<>).MakeGenericType(packetType).GetMethod("HandleAsync")!;
                    return (handler, playerState, pkt) => (Task)method.Invoke(handler, [playerState, pkt])!;
                });
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await invoker(resolvedHandler, this, packet);
                sw.Stop();
                _metrics.PacketProcessDuration.Record(sw.Elapsed.TotalMilliseconds);
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

        try
        {
            await _onDispose(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during player disconnect cleanup for session {SessionId}", SessionId);
        }

        Dispose();
    }

    private int HandleSequence(PacketFamily family, PacketAction action, ref EoReader reader)
    {
        // Init family packets: advance sequencer but never read/validate a sequence byte.
        // Matches reoserv — the Init family never includes a client sequence byte.
        if (family == PacketFamily.Init)
        {
            PacketSequencer.NextSequence();
            return 0;
        }

        if (family == PacketFamily.Connection && action == PacketAction.Ping)
        {
            PacketSequencer = PacketSequencer.WithSequenceStart(_upcomingSequence);
        }

        var serverSequence = PacketSequencer.NextSequence();
        var clientSequence = reader.GetChar();

        if (serverSequence != clientSequence)
        {
            _logger.LogWarning(
                "Sequence mismatch on {Family}_{Action} for session {SessionId}: " +
                "client={ClientSeq}, server={ServerSeq}, state={ClientState}",
                family, action, SessionId, clientSequence, serverSequence, ClientState);
        }

        // TODO: Re-enable enforcement after diagnosing sequence drift
        // if (_serverOptions.EnforceSequence && serverSequence != clientSequence)
        // {
        //     var message = $"Sending invalid sequence: Got {clientSequence}, expected {serverSequence}";
        //     CloseWithReason(message);
        //     throw new InvalidOperationException(message);
        // }

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