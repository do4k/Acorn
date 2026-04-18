using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class EvacuateCommandHandler(IAdminService adminService) : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("evacuate", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        await adminService.EvacuateMapAsync(playerState);
    }
}
