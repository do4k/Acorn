using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopSellClientPacketHandler(
    ILogger<ShopSellClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IShopDataRepository shopDataRepository,
    IInventoryService inventoryService)
    : IPacketHandler<ShopSellClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxItem = 2000000000; // ~2 billion cap

    public async Task HandleAsync(PlayerState player, ShopSellClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to sell to shop without character or map",
                player.SessionId);
            return;
        }

        var itemId = packet.SellItem.Id;
        var requestedAmount = packet.SellItem.Amount;

        if (requestedAmount <= 0 || requestedAmount > MaxItem)
        {
            logger.LogWarning("Player {Character} attempted to sell invalid amount {Amount}",
                player.Character.Name, requestedAmount);
            return;
        }

        // Validate player has the item
        var playerItemAmount = inventoryService.GetItemAmount(player.Character, itemId);
        if (playerItemAmount < requestedAmount)
        {
            logger.LogWarning("Player {Character} tried to sell {Amount}x item {ItemId} but only has {Has}",
                player.Character.Name, requestedAmount, itemId, playerItemAmount);
            return;
        }

        // Get the NPC we're interacting with
        if (player.InteractingNpcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted to sell without interacting with NPC",
                player.Character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null || npc.npc.Data.Type != NpcType.Shop)
        {
            logger.LogWarning("Player {Character} tried to sell to invalid shop NPC",
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

        // Find the trade for this item (must have sell price > 0)
        var trade = shop.Trades.FirstOrDefault(t => t.ItemId == itemId && t.SellPrice > 0);
        if (trade == null)
        {
            logger.LogWarning("Player {Character} tried to sell item {ItemId} not bought by shop {Shop}",
                player.Character.Name, itemId, shop.Name);
            return;
        }

        // Get item data
        var itemData = dataFileRepository.Eif.GetItem(itemId);
        if (itemData == null)
        {
            logger.LogWarning("Item {ItemId} not found in EIF", itemId);
            return;
        }

        // Calculate actual amount to sell (limited by what player has and shop max)
        var amount = Math.Min(requestedAmount, playerItemAmount);
        amount = Math.Min(amount, trade.MaxAmount);

        if (amount == 0)
        {
            return;
        }

        // Calculate sell value (capped at max item value)
        var sellValue = Math.Min(trade.SellPrice * amount, MaxItem);

        // Remove sold item from inventory
        if (!inventoryService.TryRemoveItem(player.Character, itemId, amount))
        {
            logger.LogWarning("Failed to remove item from player {Character}", player.Character.Name);
            return;
        }

        // Add gold to inventory
        inventoryService.TryAddItem(player.Character, GoldItemId, sellValue);

        logger.LogInformation("Player {Character} sold {Amount}x {ItemName} for {Value} gold",
            player.Character.Name, amount, itemData.Name, sellValue);

        // Send response
        await player.Send(new ShopSellServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId),
            SoldItem = new ShopSoldItem
            {
                Id = itemId,
                Amount = inventoryService.GetItemAmount(player.Character, itemId)
            },
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            }
        });
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

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ShopSellClientPacket)packet);
    }
}
