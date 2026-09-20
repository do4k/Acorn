using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class KickCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["kick"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.Guardian;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $kick <name>");
            return;
        }

        await adminService.KickPlayerAsync(playerState, args[0]);
    }
}
