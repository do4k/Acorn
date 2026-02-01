using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Bank;

public class BankOpenClientPacketHandler(
    ILogger<BankOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository)
    : IPacketHandler<BankOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BankOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open bank without character or map", player.SessionId);
            return;
        }

        // Find the NPC by index on the map
        var npcIndex = packet.NpcIndex;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null)
        {
            logger.LogWarning("Player {Character} tried to open bank at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's a bank NPC
        if (npc.npc.Data.Type != NpcType.Bank)
        {
            logger.LogWarning("Player {Character} tried to open bank at non-bank NPC {NpcId}",
                player.Character.Name, npc.npc.Id);
            return;
        }

        logger.LogInformation("Player {Character} opening bank",
            player.Character.Name);

        // Store the NPC index for subsequent deposit/withdraw operations
        player.InteractingNpcIndex = npcIndex;

        await player.Send(new BankOpenServerPacket
        {
            GoldBank = player.Character.GoldBank,
            SessionId = player.SessionId,
            LockerUpgrades = player.Character.BankMax / 5 // BankMax represents locker slots, upgrades are in increments of 5
        });
    }

}
