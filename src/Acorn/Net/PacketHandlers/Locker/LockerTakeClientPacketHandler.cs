using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Domain.Models;
using Acorn.Game.Services;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
namespace Acorn.Net.PacketHandlers.Locker;

public class LockerTakeClientPacketHandler(
    ILogger<LockerTakeClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService,
    IMapTileService mapTileService)
    : IPacketHandler<LockerTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, LockerTakeClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to take from locker without character or map", player.SessionId);
            return;
        }

        var itemId = packet.TakeItemId;

        var playerCoords = new Coords { X = player.Character.X, Y = player.Character.Y };

        // Check if player is adjacent to a bank vault tile
        var adjacentCoords = new[]
        {
            new Coords { X = playerCoords.X, Y = playerCoords.Y - 1 },
            new Coords { X = playerCoords.X, Y = playerCoords.Y + 1 },
            new Coords { X = playerCoords.X - 1, Y = playerCoords.Y },
            new Coords { X = playerCoords.X + 1, Y = playerCoords.Y }
        };

        var hasAdjacentLocker = adjacentCoords.Any(coord =>
        {
            var tile = mapTileService.GetTile(player.CurrentMap.Data, coord);
            return tile == MapTileSpec.BankVault;
        });

        if (!hasAdjacentLocker)
        {
            logger.LogWarning("Player {Character} tried to take from locker but is not adjacent to one",
                player.Character.Name);
            return;
        }

        // Get amount in bank
        var bankItem = player.Character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (bankItem == null || bankItem.Amount == 0)
        {
            logger.LogWarning("Player {Character} tried to take item {ItemId} not in locker",
                player.Character.Name, itemId);
            return;
        }

        var bankAmount = bankItem.Amount;

        // Check weight limit
        var itemData = dataFileRepository.Eif.GetItem(itemId);
        var amount = bankAmount;
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

        // Remove from bank
        RemoveBankItem(player.Character, itemId, amount);

        // Add to inventory
        inventoryService.TryAddItem(player.Character, itemId, amount);

        logger.LogInformation("Player {Character} took {Amount}x item {ItemId} from locker",
            player.Character.Name, amount, itemId);

        // Build locker items list
        var lockerItems = player.Character.Bank.Items
            .Where(i => i.Amount > 0)
            .Select(item => new ThreeItem
            {
                Id = item.Id,
                Amount = item.Amount
            }).ToList();

        await player.Send(new LockerGetServerPacket
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
            LockerItems = lockerItems
        });
    }

    private void RemoveBankItem(Domain.Models.Character character, int itemId, int amount)
    {
        var existingItem = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            existingItem.Amount -= amount;
            if (existingItem.Amount <= 0)
            {
                // Rebuild bank without this item
                var newItems = new ConcurrentBag<ItemWithAmount>(
                    character.Bank.Items.Where(i => i.Id != itemId || i.Amount > 0)
                );
                character.Bank = new Domain.Models.Bank(newItems);
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

}
