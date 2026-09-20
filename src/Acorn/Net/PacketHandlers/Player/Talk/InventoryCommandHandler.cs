using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class InventoryCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["inventory", "inv"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $inventory <name>");
            return;
        }

        await adminService.GetPlayerInventoryAsync(playerState, args[0]);
    }
}
