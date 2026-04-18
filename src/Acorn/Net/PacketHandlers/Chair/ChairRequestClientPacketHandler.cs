using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Chair;

[RequiresCharacter]
public class ChairRequestClientPacketHandler(
    ILogger<ChairRequestClientPacketHandler> logger)
    : IPacketHandler<ChairRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChairRequestClientPacket packet)
    {
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

}