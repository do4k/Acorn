using Acorn.Net.Services;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $wmt / $warpmeto - Warps the admin to another online player's location.
/// </summary>
public class WarpMeToCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("wmt", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("warpmeto", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $wmt <player>");
            return;
        }

        await adminService.WarpToPlayerAsync(playerState, args[0]);
    }
}
