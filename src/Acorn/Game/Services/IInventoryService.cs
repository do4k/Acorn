using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
/// Service responsible for managing character inventory operations.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Adds an item to the player's inventory. Stacks with existing items if possible.
    /// </summary>
    /// <returns>True if item was added, false if inventory is full</returns>
    bool TryAddItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Removes an item from the player's inventory.
    /// </summary>
    /// <returns>True if item was removed, false if player doesn't have enough</returns>
    bool TryRemoveItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Checks if player has the specified item and amount.
    /// </summary>
    bool HasItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Gets the total amount of a specific item in inventory.
    /// </summary>
    int GetItemAmount(Character character, int itemId);

    /// <summary>
    /// Gets the number of used inventory slots.
    /// </summary>
    int GetSlotCount(Character character);
}
