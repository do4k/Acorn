using Microsoft.Extensions.Logging;
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
        // AdminInteract/Tell is a "talk to admin" help request from any player.
        // The message is the help text, not a target player name.
        logger.LogInformation("Player {Character} sent an admin help request",
            player.Character?.Name);

        await adminService.SendHelpRequestAsync(player, packet.Message);
    }
}
