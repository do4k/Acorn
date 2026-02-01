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

public class LockerAddClientPacketHandler(
    ILogger<LockerAddClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService,
    IMapTileService mapTileService)
    : IPacketHandler<LockerAddClientPacket>
{
    private const int MaxItem = 2000000000;
    private const int BaseBankSize = 10;
    private const int BankSizeStep = 5;

    public async Task HandleAsync(PlayerState player, LockerAddClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to add to locker without character or map", player.SessionId);
            return;
        }

        var itemId = packet.DepositItem.Id;
        var requestedAmount = packet.DepositItem.Amount;

        // Don't allow depositing gold (ID 1) - that goes to bank
        if (itemId <= 1 || requestedAmount <= 0 || requestedAmount > MaxItem)
        {
            logger.LogWarning("Player {Character} attempted to deposit invalid item {ItemId} or amount {Amount}",
                player.Character.Name, itemId, requestedAmount);
            return;
        }

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
            logger.LogWarning("Player {Character} tried to deposit to locker but is not adjacent to one",
                player.Character.Name);
            return;
        }

        // Check bank size limit
        var bankSize = BaseBankSize + (player.Character.BankMax * BankSizeStep);
        if (player.Character.Bank.Items.Count >= bankSize)
        {
            logger.LogDebug("Player {Character}'s locker is full ({Count}/{Max})",
                player.Character.Name, player.Character.Bank.Items.Count, bankSize);

            await player.Send(new LockerSpecServerPacket
            {
                LockerMaxItems = bankSize
            });
            return;
        }

        // Get amount player actually has
        var playerAmount = inventoryService.GetItemAmount(player.Character, itemId);
        var amount = Math.Min(requestedAmount, playerAmount);

        if (amount == 0)
        {
            return;
        }

        // Remove from inventory
        if (!inventoryService.TryRemoveItem(player.Character, itemId, amount))
        {
            logger.LogWarning("Failed to remove item from player {Character}", player.Character.Name);
            return;
        }

        // Add to bank
        AddBankItem(player.Character, itemId, amount);

        logger.LogInformation("Player {Character} deposited {Amount}x item {ItemId} to locker",
            player.Character.Name, amount, itemId);

        // Build locker items list
        var lockerItems = player.Character.Bank.Items.Select(item => new ThreeItem
        {
            Id = item.Id,
            Amount = item.Amount
        }).ToList();

        await player.Send(new LockerReplyServerPacket
        {
            DepositedItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item
            {
                Id = itemId,
                Amount = inventoryService.GetItemAmount(player.Character, itemId)
            },
            Weight = new Weight
            {
                Current = CalculateCurrentWeight(player),
                Max = player.Character.MaxWeight
            },
            LockerItems = lockerItems
        });
    }

    private void AddBankItem(Domain.Models.Character character, int itemId, int amount)
    {
        var existingItem = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            existingItem.Amount += amount;
        }
        else
        {
            character.Bank.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
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
        return HandleAsync(playerState, (LockerAddClientPacket)packet);
    }
}
