using Acorn.Data;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Citizen;

public class CitizenAcceptClientPacketHandler(
    ILogger<CitizenAcceptClientPacketHandler> logger,
    IInnDataRepository innDataRepository,
    IInventoryService inventoryService)
    : IPacketHandler<CitizenAcceptClientPacket>
{
    private const int GoldItemId = 1;

    public async Task HandleAsync(PlayerState player, CitizenAcceptClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted sleep accept without character or map",
                player.SessionId);
            return;
        }

        var npcIndex = player.InteractingNpcIndex;
        if (npcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted sleep accept without interacting NPC",
                player.Character.Name);
            return;
        }

        // Check if there's a pending sleep cost
        var cost = player.SleepCost;
        if (cost == null || cost <= 0)
        {
            logger.LogWarning("Player {Character} attempted sleep accept without pending cost",
                player.Character.Name);
            return;
        }

        // Find the NPC by index on the map
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex.Value);

        if (npc.npc == null)
        {
            logger.LogWarning("Player {Character} tried to accept sleep at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's an Inn NPC
        if (npc.npc.Data.Type != NpcType.Inn)
        {
            logger.LogWarning("Player {Character} tried to accept sleep at non-inn NPC {NpcId}",
                player.Character.Name, npc.npc.Id);
            return;
        }

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.npc.Data.BehaviorId);
            return;
        }

        // Check if this is the player's home inn
        var currentHome = player.Character.Home ?? innDataRepository.DefaultHomeName;
        if (!inn.Name.Equals(currentHome, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Player {Character} tried to sleep at inn {InnName} but home is {Home}",
                player.Character.Name, inn.Name, currentHome);
            return;
        }

        // Check if player has enough gold
        var goldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId);
        if (goldAmount < cost.Value)
        {
            logger.LogWarning("Player {Character} doesn't have enough gold for sleep (has {Gold}, needs {Cost})",
                player.Character.Name, goldAmount, cost.Value);
            player.SleepCost = null;
            return;
        }

        // Remove gold
        if (!inventoryService.TryRemoveItem(player.Character, GoldItemId, cost.Value))
        {
            logger.LogWarning("Player {Character} failed to remove gold for sleep",
                player.Character.Name);
            player.SleepCost = null;
            return;
        }

        // Restore HP and TP
        player.Character.Hp = player.Character.MaxHp;
        player.Character.Tp = player.Character.MaxTp;

        // Clear sleep cost
        player.SleepCost = null;
        player.InteractingNpcIndex = null;

        logger.LogInformation("Player {Character} slept at {InnName} for {Cost} gold",
            player.Character.Name, inn.Name, cost.Value);

        // Get remaining gold
        var remainingGold = inventoryService.GetItemAmount(player.Character, GoldItemId);

        await player.Send(new CitizenAcceptServerPacket
        {
            GoldAmount = remainingGold
        });

        // TODO: Warp player to sleep location (inn.SleepMap, inn.SleepX, inn.SleepY)
        // This would require integration with the warp system
    }

}
