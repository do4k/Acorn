using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
/// Service responsible for managing character bank operations.
/// </summary>
public interface IBankService
{
    /// <summary>
    /// Adds an item to the player's bank. Stacks with existing items if possible.
    /// </summary>
    /// <returns>True if item was added, false if bank is full</returns>
    bool TryAddItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Removes an item from the player's bank.
    /// </summary>
    /// <returns>True if item was removed, false if player doesn't have enough</returns>
    bool TryRemoveItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Checks if player has the specified item and amount in bank.
    /// </summary>
    bool HasItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Gets the total amount of a specific item in the bank.
    /// </summary>
    int GetItemAmount(Character character, int itemId);
}
