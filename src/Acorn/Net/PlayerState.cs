using System.Collections.Concurrent;
using System.Diagnostics;
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
// Note: PacketSequencer from the SDK uses post-increment (matching eolib-ts for clients).
// Our custom Sequencer uses pre-increment (matching eolib-rs for servers).
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

    // Static cache for [RequiresState] attribute check per handler type
    private static readonly ConcurrentDictionary<Type, ClientState?> _requiresStateCache = new();
    
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
    private int _upcomingSequenceStart;

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
        _upcomingSequenceStart = 0;
        _logger.PlayerConnected(sessionId, communicator.GetConnectionOrigin());
        _metrics.ConnectionsTotal.Add(1);
        _metrics.PlayersOnline.Add(1);
        _onDispose = onDispose;
        _handlers = handlers;
        SessionId = sessionId;
        StartSequence = ConstrainedSequence.GenerateInitStart(Rnd);
        Communicator = communicator;
        ConnectedAt = DateTime.UtcNow;
        Task.Run(Listen);
    }

    public Random Rnd { get; } = new();

    public ClientState ClientState { get; set; } = ClientState.Uninitialized;
    public bool NeedPong { get; set; } = false;

    /// <summary>
    ///     Consecutive Connection_Player pings that went unanswered. Receiving a pong
    ///     (Connection_Ping) resets it; it disconnects once
    ///     <see cref="Options.ServerOptions.MaxMissedPings"/> is reached.
    /// </summary>
    public int MissedPings { get; set; }

    public int ClientEncryptionMulti { get; set; } = 0;
    public int ServerEncryptionMulti { get; set; } = 0;

    /// <summary>
    ///     Hardware id reported by the client during the Init handshake.
    /// </summary>
    public string? Hdid { get; set; }

    /// <summary>
    ///     Protocol revision reported in the raw Init handshake (vanilla v28 = 112).
    /// </summary>
    public int ClientProtocolVersion { get; set; } = 0;

    /// <summary>
    ///     Number of login requests received on this connection. Used to throttle
    ///     brute-force attempts.
    /// </summary>
    public int LoginAttempts { get; set; }

    /// <summary>
    ///     When the underlying transport connected, used for handshake timeouts.
    /// </summary>
    public DateTime ConnectedAt { get; }
    public Sequencer Sequencer { get; } = new(0);
    public InitSequenceStart StartSequence { get; set; }
    public ICommunicator Communicator { get; }
    public Account? Account { get; set; }
    public bool IsListeningToGlobal { get; set; }

    /// <summary>
    ///     Sequence of the most recent global message this player has already been sent.
    ///     Used so reopening the global tab only replays messages that arrived since.
    /// </summary>
    public long LastGlobalMessageSequence { get; set; }

    /// <summary>
    ///     Whether the global chat welcome has already been sent on this connection.
    /// </summary>
    public bool HasReceivedGlobalWelcome { get; set; }

    public int SessionId { get; set; }
    public WarpSession? WarpSession { get; set; }

    // Validates dialog/quest replies; kept separate from SessionId (the player's identity key)
    public int? DialogSessionId { get; set; }

    public Character? Character { get; set; }

    public MapState? CurrentMap { get; set; }

    // Spell casting state
    public int Timestamp { get; set; }
    public int? SpellId { get; set; }

    // Attack cooldown timestamp (per-connection, not shared handler state)
    public DateTime LastAttackTime { get; set; }

    // Character deletion state
    public int? CharacterIdToDelete { get; set; }

    // NPC interaction state (for shops, banks, etc.)
    public int? InteractingNpcIndex { get; set; }

    // Chest interaction state
    public Coords? InteractingChestCoords { get; set; }

    // Trade state
    public TradeSession? TradeSession { get; set; }

    /// <summary>
    ///     True while the player is in an active trade session. Item/bank/chest/locker
    ///     interactions are blocked while trading (mirrors eoserv's <c>character->trading</c> guard).
    /// </summary>
    public bool IsTrading => TradeSession is not null;

    // Pending trade request - the player who has requested to trade with us
    public int? PendingTradeRequestFromPlayerId { get; set; }

    // Admin state
    public bool IsFrozen { get; set; }

    /// <summary>
    ///     When the player's mute expires. A mute is active while this is in the future.
    /// </summary>
    public DateTime MutedUntil { get; set; }

    /// <summary>
    ///     Whether the player is currently muted (timed mute has not yet expired).
    /// </summary>
    public bool IsMuted => MutedUntil > DateTime.UtcNow;

    public bool IsJailed { get; set; }

    // Social state
    /// <summary>
    ///     Whether the player accepts private messages (whispers). Toggled by the
    ///     Global/Remove (on) and Global/Player (off) packets, mirroring eoserv.
    /// </summary>
    public bool Whispers { get; set; } = true;

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
    ///     Updates the upcoming ping sequence start value.
    ///     Called before sending a CONNECTION_PLAYER ping.
    ///     The sequencer will be updated when the client responds with CONNECTION_PING.
    /// </summary>
    public void SetUpcomingPingSequence(int start)
    {
        _upcomingSequenceStart = start;
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

                // The Init handshake carries the protocol revision as a raw byte that the
                // SDK reads and discards, so capture it here for validation in the handler.
                if (family == PacketFamily.Init && action == PacketAction.Init)
                {
                    CaptureProtocolVersion(reader);
                }

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

                // Pipeline check: reject packets sent before the connection reached the
                // state the handler requires (handshake / auth ordering).
                var requiredState = _requiresStateCache.GetOrAdd(
                    resolvedHandler.GetType(),
                    type => type.GetCustomAttribute<RequiresStateAttribute>()?.State);

                if (requiredState is { } required && ClientState < required)
                {
                    _logger.LogWarning(
                        "Player {SessionId} sent {PacketType} in state {State} before {Required}",
                        SessionId, packet.GetType().Name, ClientState, required);
                    continue;
                }

                // Use cached reflection to invoke the typed HandleAsync method
                var invoker = _handlerInvokeCache.GetOrAdd(packet.GetType(), packetType =>
                {
                    var method = typeof(IPacketHandler<>).MakeGenericType(packetType).GetMethod("HandleAsync")!;
                    return (handler, playerState, pkt) => (Task)method.Invoke(handler, [playerState, pkt])!;
                });
                
                using var activity = AcornActivitySource.Instance.StartActivity(
                    $"packet {family}/{action}", ActivityKind.Server);
                activity?.SetTag("eo.packet.family", family.ToString());
                activity?.SetTag("eo.packet.action", action.ToString());
                activity?.SetTag("eo.packet.type", packet.GetType().Name);
                activity?.SetTag("eo.session.id", SessionId);
                activity?.SetTag("enduser.id", Account?.Username);
                activity?.SetTag("acorn.character.name", Character?.Name);

                var sw = Stopwatch.StartNew();
                try
                {
                    await invoker(resolvedHandler, this, packet);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.AddException(ex);
                    throw;
                }
                finally
                {
                    sw.Stop();
                    _metrics.PacketProcessDuration.Record(sw.Elapsed.TotalMilliseconds);
                }
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

    /// <summary>
    ///     Reads the protocol revision from an Init_Init handshake without disturbing
    ///     the main reader. Layout: challenge (3), version major/minor/patch (3), protocol (1).
    /// </summary>
    private void CaptureProtocolVersion(EoReader reader)
    {
        try
        {
            var peek = reader.Slice(reader.Position);
            peek.GetThree(); // challenge
            peek.GetChar(); // version major
            peek.GetChar(); // version minor
            peek.GetChar(); // version patch
            ClientProtocolVersion = peek.GetChar();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read protocol version from Init packet for session {SessionId}",
                SessionId);
        }
    }

    private int HandleSequence(PacketFamily family, PacketAction action, ref EoReader reader)
    {
        // Skip all sequence handling when Uninitialized (matches reoserv handle_packet.rs:31).
        // The Init_Init handshake arrives before sequencing is set up.
        if (ClientState == ClientState.Uninitialized)
        {
            return 0;
        }

        // Init family packets: advance sequencer but never read/validate a sequence byte.
        // Matches reoserv — the Init family never includes a client sequence byte.
        if (family == PacketFamily.Init)
        {
            Sequencer.NextSequence();
            return 0;
        }

        // On ping response, update the sequencer start before reading the sequence.
        // Matches reoserv: sequencer.set_start(upcoming_sequence_start)
        if (family == PacketFamily.Connection && action == PacketAction.Ping)
        {
            Sequencer.SetStart(_upcomingSequenceStart);
        }

        var serverSequence = Sequencer.NextSequence();
        var clientSequence = reader.GetChar();

        if (serverSequence != clientSequence)
        {
            _logger.LogWarning(
                "Sequence mismatch on {Family}_{Action} for session {SessionId}: " +
                "client={ClientSeq}, server={ServerSeq}, state={ClientState}",
                family, action, SessionId, clientSequence, serverSequence, ClientState);
        }

        if (_serverOptions.EnforceSequence && serverSequence != clientSequence)
        {
            var message = $"Sending invalid sequence: Got {clientSequence}, expected {serverSequence}";
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

        // This is the single choke point for every reply and broadcast, so instrumenting
        // it here ties the whole cascade of packets produced by one incoming packet into
        // the same trace (and therefore the same flame graph).
        using var activity = AcornActivities.StartPacketSend(
            packet, SessionId, Account?.Username, Character?.Name);

        try
        {
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
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (ConnectionClosedException ex)
        {
            // The recipient disconnected while the packet was in flight. Sending is
            // best-effort: one departing player must not abort a broadcast to everyone
            // else (e.g. a global announcement) or take down the sender's connection.
            _logger.LogDebug(ex,
                "Dropped {Packet} for session {SessionId}: connection already closed",
                packet.GetType().Name, SessionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public void Disconnect()
    {
        _tokenSource.Cancel();
    }
}