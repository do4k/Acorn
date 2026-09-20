using Acorn.Net.Services;
using Moffat.EndlessOnline.SDK.Protocol;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class QuakeCommandHandler(IAdminService adminService) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["quake"];

    public string Usage => "[strength]";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var strength = 1;
        if (args.Length >= 1)
        {
            int.TryParse(args[0], out strength);
            if (strength < 1) strength = 1;
        }

        await adminService.TriggerQuakeAsync(playerState, strength);
    }
}
