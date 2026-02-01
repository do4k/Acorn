using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Chest;

public class ChestAddClientPacketHandler(
    ILogger<ChestAddClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<ChestAddClientPacket>
{
    private const int MaxChestItem = 2000000000;

    public async Task HandleAsync(PlayerState player, ChestAddClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to add to chest without character or map", player.SessionId);
            return;
        }

        var itemId = packet.AddItem.Id;
        var requestedAmount = packet.AddItem.Amount;

        if (itemId <= 0 || requestedAmount <= 0 || requestedAmount > MaxChestItem)
        {
            return;
        }

        // Check if player has a chest open
        if (player.InteractingChestCoords == null)
        {
            logger.LogWarning("Player {Character} attempted to add to chest without opening one",
                player.Character.Name);
            return;
        }

        var chestCoords = player.InteractingChestCoords;

        // Check if player is still in range
        var playerCoords = new Coords { X = player.Character.X, Y = player.Character.Y };
        var distance = Math.Max(Math.Abs(playerCoords.X - chestCoords.X), Math.Abs(playerCoords.Y - chestCoords.Y));
        if (distance > 1)
        {
            logger.LogWarning("Player {Character} is too far from chest", player.Character.Name);
            return;
        }

        // Get chest
        if (!player.CurrentMap.Chests.TryGetValue(chestCoords, out var chest))
        {
            logger.LogWarning("Chest not found at ({X}, {Y})", chestCoords.X, chestCoords.Y);
            return;
        }

        // Check chest capacity
        if (chest.Items.Count >= chest.MaxSlots)
        {
            logger.LogDebug("Chest is full");
            await player.Send(new ChestSpecServerPacket());
            return;
        }

        // Get amount player has
        var playerAmount = inventoryService.GetItemAmount(player.Character, itemId);
        var amount = Math.Min(requestedAmount, playerAmount);

        if (amount == 0)
        {
            return;
        }

        // Remove from player inventory
        if (!inventoryService.TryRemoveItem(player.Character, itemId, amount))
        {
            return;
        }

        // Add to chest
        var existingItem = chest.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (existingItem != null)
        {
            // Update existing - need to rebuild the bag
            var newItems = new ConcurrentBag<ChestItem>(
                chest.Items.Where(i => i.ItemId != itemId)
            );
            newItems.Add(new ChestItem(itemId, existingItem.Amount + amount));
            chest.Items = newItems;
        }
        else
        {
            chest.Items.Add(new ChestItem(itemId, amount));
        }

        logger.LogInformation("Player {Character} added {Amount}x item {ItemId} to chest",
            player.Character.Name, amount, itemId);

        // Build chest items list
        var chestItems = chest.Items.Select(item => new ThreeItem
        {
            Id = item.ItemId,
            Amount = item.Amount
        }).ToList();

        // Send response to player
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

        // Notify other players near the chest
        var agreePacket = new ChestAgreeServerPacket { Items = chestItems };
        foreach (var otherPlayer in player.CurrentMap.Players.Where(p => p != player))
        {
            if (otherPlayer.Character == null) continue;

            var otherCoords = new Coords { X = otherPlayer.Character.X, Y = otherPlayer.Character.Y };
            var otherDistance = Math.Max(Math.Abs(otherCoords.X - chestCoords.X), Math.Abs(otherCoords.Y - chestCoords.Y));
            if (otherDistance <= 1 && otherPlayer.InteractingChestCoords == chestCoords)
            {
                await otherPlayer.Send(agreePacket);
            }
        }
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
        return HandleAsync(playerState, (ChestAddClientPacket)packet);
    }
}
