using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Admin;

namespace Acorn.Net.PacketHandlers.AdminInteract;

[RequiresCharacter]
public class AdminInteractReportClientPacketHandler(
    IAdminService adminService,
    ILogger<AdminInteractReportClientPacketHandler> logger)
    : IPacketHandler<AdminInteractReportClientPacket>
{
    public async Task HandleAsync(PlayerState player, AdminInteractReportClientPacket packet)
    {
        logger.LogInformation("Player {Character} reported {Reportee}",
            player.Character?.Name, packet.Reportee);

        await adminService.SendReportAsync(player, packet.Reportee, packet.Message);
    }
}
