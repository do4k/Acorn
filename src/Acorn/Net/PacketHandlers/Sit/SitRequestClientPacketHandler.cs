using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Sit;

public class SitRequestClientPacketHandler(ILogger<SitRequestClientPacketHandler> logger)
    : IPacketHandler<SitRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, SitRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to sit without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} sitting with action {SitAction}",
            player.Character.Name, packet.SitAction);

        switch (packet.SitAction)
        {
            case SitAction.Sit:
                await player.CurrentMap.Sit(player);
                break;

            case SitAction.Stand:
                await player.CurrentMap.Stand(player);
                break;
        }
    }

}