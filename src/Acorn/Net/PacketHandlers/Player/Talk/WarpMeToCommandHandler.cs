using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $wmt / $warpmeto - Warps the admin to another online player's location.
/// </summary>
public class WarpMeToCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["wmt", "warpmeto"];

    public string Usage => "<player>";

    public AdminLevel RequiredLevel => AdminLevel.LightGuide;

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
