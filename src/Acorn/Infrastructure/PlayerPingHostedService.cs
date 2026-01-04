using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Infrastructure;

/// <summary>
///     Background service that sends periodic ping packets to all connected players
///     to keep connections alive and detect disconnected clients.
/// </summary>
public class PlayerPingHostedService(
    ILogger<PlayerPingHostedService> logger,
    WorldState worldState
) : BackgroundService
{
    private const int PingIntervalSeconds = 3;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Wait a bit before starting to allow server to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        logger.LogInformation("Player ping service started. Sending pings every {Interval} seconds",
            PingIntervalSeconds);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(PingIntervalSeconds));

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

    private async Task PingAllPlayersAsync()
    {
        var players = worldState.Players.Values.ToList();

        foreach (var player in players)
        {
            try
            {
                // Check if player needs a pong response
                if (player.NeedPong)
                {
                    logger.LogWarning("Player {SessionId} did not respond to ping, disconnecting", player.SessionId);
                    player.Dispose();
                    continue;
                }

                // Generate new ping sequence
                var upcomingSequence = PingSequenceStart.Generate(player.Rnd);

                // Store the upcoming sequence - it will be used when client responds with CONNECTION_PING
                player.SetUpcomingPingSequence(upcomingSequence);

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
}