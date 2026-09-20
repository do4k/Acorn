using Acorn.Net.Services;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $summon / $bring / $warptome - Warps another online player to the admin's location.
/// </summary>
public class SummonCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("summon", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("bring", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("warptome", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $summon <player>");
            return;
        }

        await adminService.SummonPlayerAsync(playerState, args[0]);
    }
}
