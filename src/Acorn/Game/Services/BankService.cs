using System.Collections.Concurrent;
using Acorn.Database.Models;
using Character = Acorn.Game.Models.Character;
using Bank = Acorn.Game.Models.Bank;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of bank management.
/// </summary>
public class BankService : IBankService
{
    public bool TryAddItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        // Check bank capacity
        if (character.Bank.Items.Count >= character.BankMax)
        {
            var existingItem = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
            if (existingItem == null)
            {
                return false; // Bank full and no existing stack
            }

            existingItem.Amount += amount;
            return true;
        }

        // Try to stack with existing item
        var item = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            item.Amount += amount;
            return true;
        }

        // Add new bank slot
        character.Bank.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return true;
    }

    public bool TryRemoveItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        var item = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
        {
            return false;
        }

        item.Amount -= amount;

        // Remove empty stacks
        if (item.Amount <= 0)
        {
            var newItems = new ConcurrentBag<ItemWithAmount>(
                character.Bank.Items.Where(i => i.Id != itemId || i.Amount > 0)
            );
            character.Bank = new Bank(newItems);
        }

        return true;
    }

    public bool HasItem(Character character, int itemId, int amount = 1)
    {
        var item = character.Bank.Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Amount >= amount;
    }

    public int GetItemAmount(Character character, int itemId)
    {
        return character.Bank.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;
    }
}