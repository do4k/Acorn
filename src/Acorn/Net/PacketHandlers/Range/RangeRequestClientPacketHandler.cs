using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Range;

[RequiresCharacter]
public class RangeRequestClientPacketHandler(
    ILogger<RangeRequestClientPacketHandler> logger)
    : IPacketHandler<RangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, RangeRequestClientPacket packet)
    {
        logger.LogDebug("Player {Character} requesting range data for {PlayerCount} players and {NpcCount} NPCs",
            player.Character!.Name, packet.PlayerIds.Count, packet.NpcIndexes.Count);

        if (player.CurrentMap is null)
        {
            return;
        }

        // Range/Request is answered with Range/Reply carrying a NearbyInfo restricted to
        // the requested player ids and NPC indexes, range-filtered around the requester.
        await player.Send(new RangeReplyServerPacket
        {
            Nearby = player.CurrentMap.AsNearbyInfo(player, packet.PlayerIds, packet.NpcIndexes)
        });
    }
}
