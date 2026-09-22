using Acorn.Net;
using Acorn.Net.Models;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Infrastructure;

/// <summary>
///     Background service that sends periodic ping packets to all connected players
///     to keep connections alive and detect disconnected clients.
/// </summary>
public class PlayerPingHostedService(
    ILogger<PlayerPingHostedService> logger,
    WorldState worldState,
    IOptions<ServerOptions> serverOptions
) : BackgroundService
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Wait a bit before starting to allow server to initialize
        await Task.Delay(TimeSpan.FromSeconds(_serverOptions.PlayerPingInitialDelaySeconds), cancellationToken);

        logger.LogInformation("Player ping service started. Sending pings every {Interval} seconds",
            _serverOptions.PlayerPingIntervalSeconds);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(_serverOptions.PlayerPingIntervalSeconds));

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await PingAllPlayersAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending player pings");
            }
        }
    }

    internal async Task PingAllPlayersAsync()
    {
        var players = worldState.Players.Values.ToList();

        foreach (var player in players)
        {
            try
            {
                var decision = Decide(player.ClientState, DateTime.UtcNow - player.ConnectedAt,
                    _serverOptions.HangupDelaySeconds, _serverOptions.LoginTimeoutSeconds);

                switch (decision)
                {
                    case PingDecision.DisconnectHandshakeTimeout:
                        LogReapWarningOnce(player,
                            "Player {SessionId} failed to complete handshake within {Delay}s, disconnecting",
                            _serverOptions.HangupDelaySeconds);
                        player.Disconnect();
                        ReapDeadConnection(player);
                        continue;
                    case PingDecision.DisconnectLoginTimeout:
                        LogReapWarningOnce(player,
                            "Player {SessionId} did not log in within {Delay}s, disconnecting",
                            _serverOptions.LoginTimeoutSeconds);
                        player.Disconnect();
                        ReapDeadConnection(player);
                        continue;
                    case PingDecision.Skip:
                        continue;
                    case PingDecision.Ping:
                        break;
                }

                // Check if player needs a pong response
                if (player.NeedPong)
                {
                    player.MissedPings++;
                    if (ExceededPingGrace(player.MissedPings, _serverOptions.MaxMissedPings))
                    {
                        logger.LogWarning(
                            "Player {SessionId} did not respond to {Missed} consecutive pings, disconnecting",
                            player.SessionId, player.MissedPings);
                        player.Disconnect();
                        continue;
                    }

                    // Within the grace interval: warn softly and re-probe below.
                    logger.LogDebug(
                        "Player {SessionId} has not answered {Missed} ping(s) yet, allowing grace",
                        player.SessionId, player.MissedPings);
                }

                // Generate new ping sequence
                var upcomingSequence = ConstrainedSequence.GeneratePingStart(player.Rnd);

                // Store the upcoming sequence start value — used when client responds with CONNECTION_PING
                player.SetUpcomingPingSequence(upcomingSequence.Value);

                // Send ping packet
                player.NeedPong = true;
                await player.Send(new ConnectionPlayerServerPacket
                {
                    Seq1 = upcomingSequence.Seq1,
                    Seq2 = upcomingSequence.Seq2
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error pinging player {SessionId}", player.SessionId);
            }
        }
    }

    private void LogReapWarningOnce(PlayerState player, string message, int delaySeconds)
    {
        if (player.ReapWarningLogged)
        {
            return;
        }

        player.ReapWarningLogged = true;
        logger.LogWarning(message, player.SessionId, delaySeconds);
    }

    /// <summary>
    ///     Last-resort sweep for connections whose transport is already gone but whose session
    ///     is still registered: cancelling the token cannot help when the read loop has already
    ///     finished (or raced registration), so remove the world entry and let the player
    ///     dispose its side-effects (metrics, socket close) exactly once. Reap-only: sessions
    ///     still holding a live transport are left to their normal disconnect path. A reaped
    ///     pre-authentication connection has no character to persist.
    /// </summary>
    private void ReapDeadConnection(PlayerState player)
    {
        if (player.Communicator.IsConnected)
        {
            return;
        }

        worldState.TryRemovePlayer(player.SessionId, player);
        player.Dispose();
    }

    /// <summary>
    ///     Whether the number of consecutive unanswered pings has grown past the configured
    ///     grace interval and the connection should now be dropped.
    /// </summary>
    internal static bool ExceededPingGrace(int missedPings, int maxMissedPings)
    {
        return missedPings >= maxMissedPings;
    }

    /// <summary>
    ///     Decides what to do with a connection on the current tick.
    /// </summary>
    internal static PingDecision Decide(ClientState state, TimeSpan age, int hangupDelaySeconds,
        int loginTimeoutSeconds)
    {
        // Handshake: Init -> Connection/Accept. The client performs this automatically, so
        // a connection that has not got this far within the hangup delay is not a player.
        if (state < ClientState.Accepted && hangupDelaySeconds > 0 && age.TotalSeconds > hangupDelaySeconds)
        {
            return PingDecision.DisconnectHandshakeTimeout;
        }

        // Login: Account/Login. This needs a human, so allow a much longer grace period
        // before dropping the still-unauthenticated connection.
        if (state < ClientState.LoggedIn && loginTimeoutSeconds > 0 && age.TotalSeconds > loginTimeoutSeconds)
        {
            return PingDecision.DisconnectLoginTimeout;
        }

        // Never ping a connection that is still authenticating. The ping carries a
        // sequence start that resets the client sequencer, which can corrupt the login
        // exchange, and a half-open socket would otherwise stay alive forever as long as
        // it answered pings.
        return state < ClientState.LoggedIn ? PingDecision.Skip : PingDecision.Ping;
    }

    /// <summary>
    ///     What the ping service should do with a connection on the current tick.
    /// </summary>
    internal enum PingDecision
    {
        /// <summary>
        ///     The connection is still inside a grace period; leave it alone.
        /// </summary>
        Skip,

        /// <summary>
        ///     The connection never completed the Init/Accept handshake.
        /// </summary>
        DisconnectHandshakeTimeout,

        /// <summary>
        ///     The connection completed the handshake but never logged in.
        /// </summary>
        DisconnectLoginTimeout,

        /// <summary>
        ///     The connection is authenticated and should receive a keep-alive ping.
        /// </summary>
        Ping
    }
}
