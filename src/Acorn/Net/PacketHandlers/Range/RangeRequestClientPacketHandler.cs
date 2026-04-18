using Acorn.Extensions;
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
            player.Character.Name, packet.PlayerIds.Count, packet.NpcIndexes.Count);

        // Send player data if requested
        if (packet.PlayerIds.Count > 0)
        {
            await player.Send(new PlayersListServerPacket
            {
                PlayersList = new PlayersList
                {
                    Players = player.CurrentMap.Players.Values
                        .Where(p => p.Character != null && packet.PlayerIds.Contains(p.SessionId))
                        .Select(p => p.Character!.AsOnlinePlayer())
                        .ToList()
                }
            });
        }

        // Send NPC data if requested
        if (packet.NpcIndexes.Count > 0)
        {
            var npcs = player.CurrentMap.AsNpcMapInfo()
                .Where((npc, index) => packet.NpcIndexes.Contains(index))
                .ToList();

            await player.Send(new NpcAgreeServerPacket
            {
                Npcs = npcs
            });
        }
    }

}