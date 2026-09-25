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

        // Block chest deposits while trading (mirrors eoserv Chest.cpp)
        if (player.IsTrading)
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

        // Serialize chest mutations so concurrent adds (or an add racing a take)
        // can't lose stacks or exceed caps.
        int depositedAmount = 0;
        List<ThreeItem>? updatedItems = null;
        lock (chest)
        {
            var existingItem = chest.Items.FirstOrDefault(i => i.ItemId == itemId);
            var existingAmount = existingItem?.Amount ?? 0;

            // Slot cap only applies when a new stack would be created.
            if (existingItem is null && chest.Items.Count >= chest.MaxSlots)
            {
                logger.LogDebug("Chest at ({X}, {Y}) is full", chestCoords.X, chestCoords.Y);
            }
            else
            {
                // Per-item cap (matches eoserv: amount = min(requested, MaxChest - existing)).
                var capacity = MaxChestItem - existingAmount;
                if (capacity <= 0)
                {
                    logger.LogDebug("Chest at ({X}, {Y}) is full", chestCoords.X, chestCoords.Y);
                }
                else
                {
                    var playerAmount = inventoryService.GetItemAmount(player.Character, itemId);
                    var amount = Math.Min(requestedAmount, Math.Min(playerAmount, capacity));

                    if (amount > 0 && inventoryService.TryRemoveItem(player.Character, itemId, amount))
                    {
                        if (existingItem is not null)
                        {
                            chest.Items = new ConcurrentBag<ChestItem>(chest.Items.Where(i => i.ItemId != itemId))
                            {
                                new(itemId, existingAmount + amount)
                            };
                        }
                        else
                        {
                            chest.Items.Add(new ChestItem(itemId, amount));
                        }

                        depositedAmount = amount;
                        updatedItems = chestService.ToThreeItems(chest);
                        logger.LogInformation("Player {Character} added {Amount}x item {ItemId} to chest",
                            player.Character.Name, amount, itemId);
                    }
                }
            }
        }

        if (depositedAmount == 0 || updatedItems is null)
        {
            // Chest full, at cap, nothing to deposit, or inventory changed under us.
            await player.Send(new ChestSpecServerPacket());
            return;
        }

        await player.Send(new ChestReplyServerPacket
        {
            AddedItemId = itemId,
            RemainingAmount = inventoryService.GetItemAmount(player.Character, itemId),
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            },
            Items = updatedItems
        });

        // Notify nearby observers (not just players who have this chest open).
        var agreePacket = new ChestAgreeServerPacket { Items = updatedItems };
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
