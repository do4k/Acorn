using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Chair;

public class ChairRequestClientPacketHandler(
    ILogger<ChairRequestClientPacketHandler> logger)
    : IPacketHandler<ChairRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChairRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to sit in chair without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} chair action {SitAction}",
            player.Character.Name, packet.SitAction);

        switch (packet.SitAction)
        {
            case SitAction.Sit:
                // Extract coordinates from SitActionData
                if (packet.SitActionData is ChairRequestClientPacket.SitActionDataSit sitData)
                {
                    await player.CurrentMap.SitInChair(player, sitData.Coords);
                }

                break;

            case SitAction.Stand:
                await player.CurrentMap.StandFromChair(player);
                break;
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ChairRequestClientPacket)packet);
    }
}