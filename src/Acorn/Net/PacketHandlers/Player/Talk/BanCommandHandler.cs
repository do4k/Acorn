using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class BanCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["ban", "sban"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $ban <name>");
            return;
        }

        var silent = command.Equals("sban", StringComparison.InvariantCultureIgnoreCase);
        await adminService.BanPlayerAsync(playerState, args[0], silent);
    }
}
