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
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Chest;

[RequiresCharacter]
public class ChestAddClientPacketHandler(
    ILogger<ChestAddClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService,
    IChestService chestService)
    : IPacketHandler<ChestAddClientPacket>
{
    /// <summary>Maximum amount of a single item stack a chest may hold (matches eoserv's MaxChest).</summary>
    private const int MaxChestItem = 2000000000;

    public async Task HandleAsync(PlayerState player, ChestAddClientPacket packet)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        // Trading players must not mutate chest contents.
        if (player.TradeSession is not null)
        {
            logger.LogWarning("Player {Character} attempted to add to a chest while trading",
                player.Character.Name);
            return;
        }

        var itemId = packet.AddItem.Id;
        var requestedAmount = packet.AddItem.Amount;

        if (itemId <= 0 || requestedAmount <= 0 || requestedAmount > MaxChestItem)
        {
            return;
        }

        var chestCoords = packet.Coords;
        var map = player.CurrentMap;

        if (!chestService.IsInBounds(map, chestCoords) || !chestService.IsChestTile(map, chestCoords))
        {
            logger.LogWarning("Player {Character} attempted to add to a non-chest tile ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        if (!chestService.IsAdjacent(player.Character, chestCoords))
        {
            logger.LogWarning("Player {Character} is too far from chest ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        var itemData = dataFileRepository.Eif.GetItem(itemId);
        if (itemData is null)
        {
            return;
        }

        // Lore items can never be stored in a chest.
        if (itemData.Special == ItemSpecial.Lore)
        {
            logger.LogDebug("Player {Character} tried to store Lore item {ItemId} in a chest",
                player.Character.Name, itemId);
            return;
        }

        var chest = chestService.GetOrCreateChest(map, chestCoords);
        var existingItem = chest.Items.FirstOrDefault(i => i.ItemId == itemId);
        var existingAmount = existingItem?.Amount ?? 0;

        // Slot cap only applies when a new stack would be created.
        if (existingItem is null && chest.Items.Count >= chest.MaxSlots)
        {
            logger.LogDebug("Chest at ({X}, {Y}) is full", chestCoords.X, chestCoords.Y);
            await player.Send(new ChestSpecServerPacket());
            return;
        }

        // Per-item cap (matches eoserv: amount = min(requested, MaxChest - existing)).
        var capacity = MaxChestItem - existingAmount;
        if (capacity <= 0)
        {
            await player.Send(new ChestSpecServerPacket());
            return;
        }

        var playerAmount = inventoryService.GetItemAmount(player.Character, itemId);
        var amount = Math.Min(requestedAmount, Math.Min(playerAmount, capacity));

        if (amount <= 0)
        {
            return;
        }

        if (!inventoryService.TryRemoveItem(player.Character, itemId, amount))
        {
            return;
        }

        if (existingItem is not null)
        {
            var newItems = new ConcurrentBag<ChestItem>(chest.Items.Where(i => i.ItemId != itemId))
            {
                new(itemId, existingAmount + amount)
            };
            chest.Items = newItems;
        }
        else
        {
            chest.Items.Add(new ChestItem(itemId, amount));
        }

        logger.LogInformation("Player {Character} added {Amount}x item {ItemId} to chest",
            player.Character.Name, amount, itemId);

        var chestItems = chestService.ToThreeItems(chest);

        await player.Send(new ChestReplyServerPacket
        {
            AddedItemId = itemId,
            RemainingAmount = inventoryService.GetItemAmount(player.Character, itemId),
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
