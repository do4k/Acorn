using Acorn.Extensions;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Range;

public class RangeRequestClientPacketHandler(
    ILogger<RangeRequestClientPacketHandler> logger,
    IWorldQueries worldQueries)
    : IPacketHandler<RangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, RangeRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted range request without character or map", player.SessionId);
            return;
        }

        logger.LogDebug("Player {Character} requesting range data for {PlayerCount} players and {NpcCount} NPCs",
            player.Character.Name, packet.PlayerIds.Count, packet.NpcIndexes.Count);

        // Send player data if requested
        if (packet.PlayerIds.Count > 0)
        {
            await player.Send(new PlayersListServerPacket
            {
                PlayersList = new PlayersList
                {
                    Players = player.CurrentMap.Players
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

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (RangeRequestClientPacket)packet);
    }
}