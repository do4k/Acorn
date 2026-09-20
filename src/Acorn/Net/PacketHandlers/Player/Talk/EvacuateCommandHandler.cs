using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class EvacuateCommandHandler(IAdminService adminService) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["evacuate"];

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        await adminService.EvacuateMapAsync(playerState);
    }
}
