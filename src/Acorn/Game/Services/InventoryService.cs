using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of inventory management.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IDataFileRepository? _dataRepository;
    private readonly IWeightCalculator _weightCalculator;

    public InventoryService(IWeightCalculator? weightCalculator = null, IDataFileRepository? dataRepository = null)
    {
        _weightCalculator = weightCalculator ?? new WeightCalculator();
        _dataRepository = dataRepository;
    }

    public bool TryAddItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        // Enforce the carry weight limit when the item database is available.
        if (_dataRepository != null && !CanHoldItem(character, _dataRepository.Eif, itemId, amount))
        {
            return false;
        }

        // Try to find existing stack of this item
        var existingItem = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            // Stack with existing item
            existingItem.Amount += amount;
            return true;
        }

        // Add new inventory slot
        // Note: No hard slot limit enforced here, limited by 2000 char serialization
        character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return true;
    }

    public bool CanHoldItem(Character character, Eif items, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        var itemData = items.GetItem(itemId);
        // Unknown items and weightless items can always be held.
        if (itemData == null || itemData.Weight <= 0)
        {
            return true;
        }

        return _weightCalculator.CanCarry(character, items, itemId, amount);
    }

    public bool TryRemoveItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        var item = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
        {
            return false;
        }

        item.Amount -= amount;

        // Remove empty stacks
        if (item.Amount <= 0)
        {
            // ConcurrentBag doesn't have Remove, so we rebuild without this item
            var newItems = new ConcurrentBag<ItemWithAmount>(
                character.Inventory.Items.Where(i => i.Id != itemId || i.Amount > 0)
            );
            character.Inventory = new Inventory(newItems);
        }

        return true;
    }

    public bool HasItem(Character character, int itemId, int amount = 1)
    {
        var item = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Amount >= amount;
    }

    public int GetItemAmount(Character character, int itemId)
    {
        return character.Inventory.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;
    }

    public int GetSlotCount(Character character)
    {
        return character.Inventory.Items.Count;
    }
}