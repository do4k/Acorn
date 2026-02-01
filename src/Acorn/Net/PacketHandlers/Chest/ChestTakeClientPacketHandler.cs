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

public class ChestTakeClientPacketHandler(
    ILogger<ChestTakeClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<ChestTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChestTakeClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to take from chest without character or map", player.SessionId);
            return;
        }

        var itemId = packet.TakeItemId;

        // Check if player has a chest open
        if (player.InteractingChestCoords == null)
        {
            logger.LogWarning("Player {Character} attempted to take from chest without opening one",
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

        // Find item in chest
        var chestItem = chest.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (chestItem == null)
        {
            logger.LogWarning("Item {ItemId} not found in chest", itemId);
            return;
        }

        // Check weight limit
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

        // Remove from chest
        if (amount >= chestItem.Amount)
        {
            // Remove entire item
            chest.Items = new ConcurrentBag<ChestItem>(
                chest.Items.Where(i => i.ItemId != itemId)
            );
        }
        else
        {
            // Reduce amount
            chest.Items = new ConcurrentBag<ChestItem>(
                chest.Items.Where(i => i.ItemId != itemId)
            );
            chest.Items.Add(new ChestItem(itemId, chestItem.Amount - amount));
        }

        // Add to player inventory
        inventoryService.TryAddItem(player.Character, itemId, amount);

        logger.LogInformation("Player {Character} took {Amount}x item {ItemId} from chest",
            player.Character.Name, amount, itemId);

        // Build chest items list
        var chestItems = chest.Items.Select(item => new ThreeItem
        {
            Id = item.ItemId,
            Amount = item.Amount
        }).ToList();

        // Send response to player
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
        return HandleAsync(playerState, (ChestTakeClientPacket)packet);
    }
}
