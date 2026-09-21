using Acorn.Net.Services;
using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class JailCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["jail", "sjail"];

    public string Usage => "<name>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $jail <name>");
            return;
        }

        var silent = command.Equals("sjail", StringComparison.InvariantCultureIgnoreCase);
        await adminService.JailPlayerAsync(playerState, args[0], silent);
    }
}
