using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Barber;

[RequiresCharacter]
public class BarberOpenClientPacketHandler(
    ILogger<BarberOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository)
    : IPacketHandler<BarberOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BarberOpenClientPacket packet)
    {
        var npcIndex = packet.NpcIndex;
        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc))
        {
            logger.LogWarning("Player {Character} tried to open barber at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's a barber NPC
        if (npc.Data.Type != NpcType.Barber)
        {
            logger.LogWarning("Player {Character} tried to open barber at non-barber NPC {NpcId}",
                player.Character.Name, npc.Id);
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
