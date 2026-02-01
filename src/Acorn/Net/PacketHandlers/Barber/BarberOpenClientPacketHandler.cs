using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Barber;

public class BarberOpenClientPacketHandler(
    ILogger<BarberOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository)
    : IPacketHandler<BarberOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BarberOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open barber without character or map", player.SessionId);
            return;
        }

        var npcIndex = packet.NpcIndex;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null)
        {
            logger.LogWarning("Player {Character} tried to open barber at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's a barber NPC
        if (npc.npc.Data.Type != NpcType.Barber)
        {
            logger.LogWarning("Player {Character} tried to open barber at non-barber NPC {NpcId}",
                player.Character.Name, npc.npc.Id);
            return;
        }

        logger.LogInformation("Player {Character} opening barber", player.Character.Name);

        // Store the NPC index for subsequent buy operations
        player.InteractingNpcIndex = npcIndex;

        await player.Send(new BarberOpenServerPacket
        {
            SessionId = player.SessionId
        });
    }

}
