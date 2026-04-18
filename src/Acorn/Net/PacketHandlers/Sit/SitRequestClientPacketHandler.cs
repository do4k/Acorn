using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Sit;

[RequiresCharacter]
public class SitRequestClientPacketHandler(ILogger<SitRequestClientPacketHandler> logger)
    : IPacketHandler<SitRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, SitRequestClientPacket packet)
    {
        logger.LogInformation("Player {Character} sitting with action {SitAction}",
            player.Character.Name, packet.SitAction);

        // TODO: Implement map.PlayerSit(player, sitAction, cursor)
        await Task.CompletedTask;
    }

}