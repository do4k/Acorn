using Acorn.Infrastructure;
using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $uptime - Shows how long the server has been running.
/// </summary>
public class UptimeCommandHandler(IServerStatusService serverStatus, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("uptime", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var uptime = serverStatus.Uptime;
        var message = $"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        await notifications.SystemMessage(playerState, message);
    }
}
