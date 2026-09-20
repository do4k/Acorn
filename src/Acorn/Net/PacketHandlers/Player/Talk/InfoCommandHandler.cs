using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class InfoCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["info", "player"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.LightGuide;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $info <name>");
            return;
        }

        await adminService.GetPlayerInfoAsync(playerState, args[0]);
    }
}
