using Acorn.Net.Services;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class InfoCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("info", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("player", StringComparison.InvariantCultureIgnoreCase);

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
