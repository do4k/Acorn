using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class UnfreezeCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["unfreeze"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.Guardian;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $unfreeze <name>");
            return;
        }

        await adminService.UnfreezePlayerAsync(playerState, args[0]);
    }
}
