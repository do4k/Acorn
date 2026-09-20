using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $summon / $bring / $warptome - Warps another online player to the admin's location.
/// </summary>
public class SummonCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["summon", "bring", "warptome"];

    public string Usage => "<player>";

    public AdminLevel RequiredLevel => AdminLevel.Guardian;

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
