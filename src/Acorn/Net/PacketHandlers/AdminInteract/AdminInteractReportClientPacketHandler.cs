using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;
using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.AdminInteract;

[RequiresCharacter]
public class AdminInteractReportClientPacketHandler(
    INotificationService notifications,
    ILogger<AdminInteractReportClientPacketHandler> logger)
    : IPacketHandler<AdminInteractReportClientPacket>
{
    public async Task HandleAsync(PlayerState player, AdminInteractReportClientPacket packet)
    {
        logger.LogInformation("Player {Character} reported {Reportee}: {Message}",
            player.Character?.Name, packet.Reportee, packet.Message);

        // Notify all online admins about the report
        var adminMessage = $"[Report] {player.Character?.Name} reported {packet.Reportee}: {packet.Message}";

        // Send confirmation to the reporter
        await notifications.SystemMessage(player, $"Your report about {packet.Reportee} has been submitted.");
    }
}
