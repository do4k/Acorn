using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Chest;

[RequiresCharacter]
public class ChestTakeClientPacketHandler(
    ILogger<ChestTakeClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService,
    IChestService chestService)
    : IPacketHandler<ChestTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChestTakeClientPacket packet)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        // Block chest withdrawals while trading (mirrors eoserv Chest.cpp)
        if (player.IsTrading)
        {
            logger.LogWarning("Player {Character} attempted to take from a chest while trading",
                player.Character.Name);
            return;
        }

        var itemId = packet.TakeItemId;
        var chestCoords = packet.Coords;
        var map = player.CurrentMap;

        if (!chestService.IsInBounds(map, chestCoords) || !chestService.IsChestTile(map, chestCoords))
        {
            logger.LogWarning("Player {Character} attempted to take from a non-chest tile ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        if (!chestService.IsAdjacent(player.Character, chestCoords))
        {
            logger.LogWarning("Player {Character} is too far from chest ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        if (!map.Chests.TryGetValue(chestCoords, out var chest))
        {
            logger.LogWarning("Chest not found at ({X}, {Y})", chestCoords.X, chestCoords.Y);
            return;
        }

        var chestItem = chest.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (chestItem == null)
        {
            logger.LogWarning("Item {ItemId} not found in chest", itemId);
            return;
        }

        // Limit by available carry weight.
        var itemData = dataFileRepository.Eif.GetItem(itemId);
        var amount = chestItem.Amount;
        if (itemData != null && itemData.Weight > 0)
        {
            var currentWeight = CalculateCurrentWeight(player);
            var availableWeight = player.Character.MaxWeight - currentWeight;
            var canHold = availableWeight / itemData.Weight;
            amount = Math.Min(amount, canHold);
        }

        if (amount == 0)
        {
            logger.LogDebug("Player {Character} cannot hold any more of item {ItemId} (weight limit)",
                player.Character.Name, itemId);
            return;
        }

        if (amount >= chestItem.Amount)
        {
            chest.Items = new ConcurrentBag<ChestItem>(chest.Items.Where(i => i.ItemId != itemId));
        }
        else
        {
            chest.Items = new ConcurrentBag<ChestItem>(chest.Items.Where(i => i.ItemId != itemId))
            {
                new(itemId, chestItem.Amount - amount)
            };
        }

        inventoryService.TryAddItem(player.Character, itemId, amount);

        logger.LogInformation("Player {Character} took {Amount}x item {ItemId} from chest",
            player.Character.Name, amount, itemId);

        var chestItems = chestService.ToThreeItems(chest);

        await player.Send(new ChestGetServerPacket
        {
            TakenItem = new ThreeItem
            {
                Id = itemId,
                Amount = amount
            },
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            },
            Items = chestItems
        });

        // Notify nearby observers (not just players who have this chest open).
        var agreePacket = new ChestAgreeServerPacket { Items = chestItems };
        foreach (var observer in chestService.GetNearbyObservers(map, chestCoords, player))
        {
            await observer.Send(agreePacket);
        }
    }

    private int CalculateCurrentWeight(PlayerState player)
    {
        if (player.Character == null)
        {
            return 0;
        }

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
