using Acorn.Net.Services;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class UnmuteCommandHandler(IAdminService adminService, INotificationService notifications) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("unmute", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await notifications.SystemMessage(playerState, "Usage: $unmute <name>");
            return;
        }

        await adminService.UnmutePlayerAsync(playerState, args[0]);
    }
}
