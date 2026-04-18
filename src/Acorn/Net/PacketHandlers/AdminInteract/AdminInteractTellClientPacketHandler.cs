using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.AdminInteract;

[RequiresCharacter]
public class AdminInteractTellClientPacketHandler(
    IAdminService adminService,
    ILogger<AdminInteractTellClientPacketHandler> logger)
    : IPacketHandler<AdminInteractTellClientPacket>
{
    public async Task HandleAsync(PlayerState player, AdminInteractTellClientPacket packet)
    {
        if (player.Character?.Admin <= AdminLevel.Player)
        {
            logger.LogWarning("Non-admin player {Character} attempted admin interact tell",
                player.Character?.Name);
            return;
        }

        logger.LogInformation("Admin {Character} requesting info via admin interact: {Message}",
            player.Character!.Name, packet.Message);

        // The message field contains the target player name
        await adminService.GetPlayerInfoAsync(player, packet.Message);
    }
}
