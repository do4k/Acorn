using System.Collections.Concurrent;
using Acorn.Database.Models;
using Character = Acorn.Game.Models.Character;
using Inventory = Acorn.Game.Models.Inventory;

namespace Acorn.Game.Services;

/// <summary>
/// Default implementation of inventory management.
/// </summary>
public class InventoryService : IInventoryService
{
    public bool TryAddItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

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

    public bool TryRemoveItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0) return false;

        var item = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
            return false;

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
