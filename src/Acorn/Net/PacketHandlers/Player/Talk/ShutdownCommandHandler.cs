using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Microsoft.Extensions.Hosting;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $shutdown [reason] - Broadcasts a server-wide notice and requests a graceful
///     host stop. Online characters are persisted by the shutdown persistence
///     hosted service while connections are still open.
/// </summary>
public class ShutdownCommandHandler(
    IAdminService adminService,
    IHostApplicationLifetime lifetime,
    INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["shutdown"];

    public string Usage => "[reason]";

    public AdminLevel RequiredLevel => AdminLevel.HighGameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var reason = args.Length > 0 ? string.Join(' ', args) : "Server is shutting down.";

        await adminService.GlobalMessageAsync(playerState, reason);
        await notifications.SystemMessage(playerState, "Shutdown started - saving online characters.");

        lifetime.StopApplication();
    }
}
