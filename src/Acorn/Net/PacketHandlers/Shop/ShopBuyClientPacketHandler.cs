using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopBuyClientPacketHandler(
    ILogger<ShopBuyClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IShopDataRepository shopDataRepository,
    IInventoryService inventoryService)
    : IPacketHandler<ShopBuyClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxItem = 2000000000; // ~2 billion cap

    public async Task HandleAsync(PlayerState player, ShopBuyClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to buy from shop without character or map",
                player.SessionId);
            return;
        }

        var itemId = packet.BuyItem.Id;
        var requestedAmount = packet.BuyItem.Amount;

        if (requestedAmount <= 0 || requestedAmount > MaxItem)
        {
            logger.LogWarning("Player {Character} attempted to buy invalid amount {Amount}",
                player.Character.Name, requestedAmount);
            return;
        }

        // Get the NPC we're interacting with
        if (player.InteractingNpcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted to buy without interacting with NPC",
                player.Character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null || npc.npc.Data.Type != NpcType.Shop)
        {
            logger.LogWarning("Player {Character} tried to buy from invalid shop NPC",
                player.Character.Name);
            return;
        }

        // Get shop data
        var shop = shopDataRepository.GetShopByBehaviorId(npc.npc.Data.BehaviorId);
        if (shop == null)
        {
            logger.LogWarning("No shop data found for NPC behavior ID {BehaviorId}", npc.npc.Data.BehaviorId);
            return;
        }

        // Find the trade for this item
        var trade = shop.Trades.FirstOrDefault(t => t.ItemId == itemId && t.BuyPrice > 0);
        if (trade == null)
        {
            logger.LogWarning("Player {Character} tried to buy item {ItemId} not sold by shop {Shop}",
                player.Character.Name, itemId, shop.Name);
            return;
        }

        // Get item data for weight calculation
        var itemData = dataFileRepository.Eif.GetItem(itemId);
        if (itemData == null)
        {
            logger.LogWarning("Item {ItemId} not found in EIF", itemId);
            return;
        }

        // Calculate how many the player can actually hold (weight limit)
        var canHold = CalculateCanHold(player, itemData, requestedAmount);
        if (canHold == 0)
        {
            logger.LogDebug("Player {Character} cannot hold any more of item {ItemId}",
                player.Character.Name, itemId);
            return;
        }

        // Limit to max amount from shop
        var amount = Math.Min(canHold, trade.MaxAmount);
        amount = Math.Min(amount, requestedAmount);

        // Calculate total cost
        var totalCost = trade.BuyPrice * amount;

        // Check if player has enough gold
        var playerGold = inventoryService.GetItemAmount(player.Character, GoldItemId);
        if (playerGold < totalCost)
        {
            logger.LogDebug("Player {Character} doesn't have enough gold ({Gold}) for purchase ({Cost})",
                player.Character.Name, playerGold, totalCost);
            return;
        }

        // Remove gold
        if (!inventoryService.TryRemoveItem(player.Character, GoldItemId, totalCost))
        {
            logger.LogWarning("Failed to remove gold from player {Character}", player.Character.Name);
            return;
        }

        // Add purchased item
        inventoryService.TryAddItem(player.Character, itemId, amount);

        logger.LogInformation("Player {Character} bought {Amount}x {ItemName} for {Cost} gold",
            player.Character.Name, amount, itemData.Name, totalCost);

        // Send response
        await player.Send(new ShopBuyServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId),
            BoughtItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item
            {
                Id = itemId,
                Amount = amount
            },
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            }
        });
    }

    private int CalculateCanHold(PlayerState player, EifRecord itemData, int requestedAmount)
    {
        if (player.Character == null) return 0;

        var currentWeight = CalculateCurrentWeight(player);
        var maxWeight = player.Character.MaxWeight;
        var availableWeight = maxWeight - currentWeight;

        if (itemData.Weight <= 0) return requestedAmount;

        return Math.Min(requestedAmount, availableWeight / itemData.Weight);
    }

    private int CalculateCurrentWeight(PlayerState player)
    {
        if (player.Character == null) return 0;

        var totalWeight = 0;
        foreach (var item in player.Character.Inventory.Items)
        {
            var itemData = dataFileRepository.Eif.GetItem(item.Id);
            if (itemData != null)
            {
                totalWeight += itemData.Weight * item.Amount;
            }
        }

        return totalWeight;
    }

}
