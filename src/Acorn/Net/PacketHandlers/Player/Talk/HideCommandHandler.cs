using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class HideCommandHandler(IAdminService adminService) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("hide", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("show", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        await adminService.ToggleHideAsync(playerState);
    }
}
