using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopCreateClientPacketHandler(
    ILogger<ShopCreateClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IShopDataRepository shopDataRepository,
    IInventoryService inventoryService)
    : IPacketHandler<ShopCreateClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopCreateClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to craft without character or map", player.SessionId);
            return;
        }

        var craftItemId = packet.CraftItemId;

        // Get the NPC we're interacting with
        if (player.InteractingNpcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted to craft without interacting with NPC",
                player.Character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null || npc.npc.Data.Type != NpcType.Shop)
        {
            logger.LogWarning("Player {Character} tried to craft at invalid shop NPC",
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

        // Find the craft recipe for this item
        var craft = shop.Crafts.FirstOrDefault(c => c.ItemId == craftItemId);
        if (craft == null)
        {
            logger.LogWarning("Player {Character} tried to craft item {ItemId} not craftable at shop {Shop}",
                player.Character.Name, craftItemId, shop.Name);
            return;
        }

        // Check if player has all required ingredients
        foreach (var ingredient in craft.Ingredients)
        {
            if (ingredient.ItemId > 0 &&
                inventoryService.GetItemAmount(player.Character, ingredient.ItemId) < ingredient.Amount)
            {
                logger.LogDebug("Player {Character} missing ingredient {ItemId} for craft",
                    player.Character.Name, ingredient.ItemId);
                return;
            }
        }

        // Remove all ingredients
        foreach (var ingredient in craft.Ingredients)
        {
            if (ingredient.ItemId > 0)
            {
                if (!inventoryService.TryRemoveItem(player.Character, ingredient.ItemId, ingredient.Amount))
                {
                    logger.LogError("Failed to remove ingredient {ItemId} from player {Character}",
                        ingredient.ItemId, player.Character.Name);
                    return;
                }
            }
        }

        // Add crafted item
        inventoryService.TryAddItem(player.Character, craftItemId, 1);

        var itemData = dataFileRepository.Eif.GetItem(craftItemId);
        logger.LogInformation("Player {Character} crafted {ItemName}",
            player.Character.Name, itemData?.Name ?? $"Item {craftItemId}");

        // Build response with remaining ingredient amounts
        // Pad to exactly 4 ingredients
        var ingredientAmounts = craft.Ingredients.Take(4).ToList();
        while (ingredientAmounts.Count < 4)
        {
            ingredientAmounts.Add(new ShopCraftIngredient(0, 0));
        }

        await player.Send(new ShopCreateServerPacket
        {
            CraftItemId = craftItemId,
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            },
            Ingredients =
            [
                new Moffat.EndlessOnline.SDK.Protocol.Net.Item
                {
                    Id = ingredientAmounts[0].ItemId,
                    Amount = inventoryService.GetItemAmount(player.Character, ingredientAmounts[0].ItemId)
                },
                new Moffat.EndlessOnline.SDK.Protocol.Net.Item
                {
                    Id = ingredientAmounts[1].ItemId,
                    Amount = inventoryService.GetItemAmount(player.Character, ingredientAmounts[1].ItemId)
                },
                new Moffat.EndlessOnline.SDK.Protocol.Net.Item
                {
                    Id = ingredientAmounts[2].ItemId,
                    Amount = inventoryService.GetItemAmount(player.Character, ingredientAmounts[2].ItemId)
                },
                new Moffat.EndlessOnline.SDK.Protocol.Net.Item
                {
                    Id = ingredientAmounts[3].ItemId,
                    Amount = inventoryService.GetItemAmount(player.Character, ingredientAmounts[3].ItemId)
                }
            ]
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

}
