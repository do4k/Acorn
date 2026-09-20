using Acorn.World.Services.Admin;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class HideCommandHandler(IAdminService adminService) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["hide", "show"];

    public AdminLevel RequiredLevel => AdminLevel.Guardian;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        await adminService.ToggleHideAsync(playerState);
    }
}
