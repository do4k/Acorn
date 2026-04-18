using Acorn.Net.Services;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class BanCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("ban", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $ban <name>");
            return;
        }

        await adminService.BanPlayerAsync(playerState, args[0]);
    }
}
