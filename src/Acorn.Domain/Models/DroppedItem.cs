namespace Acorn.Domain.Models;

/// <summary>
///     Represents an item dropped on the ground.
///     Items can be looted by players, with ownership protection in the early stages after drop.
/// </summary>
public class DroppedItem
{
    public DroppedItem()
    {
        DroppedTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     The ID of the item in the item database
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    ///     Amount of this item dropped (1 for single items)
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     X coordinate on the map
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y coordinate on the map
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Session ID of the player who killed the NPC and earned the drop.
    ///     0 means no owner (free for all to loot)
    /// </summary>
    public int Owner { get; set; }

    /// <summary>
    ///     Number of game ticks remaining before this item becomes available to all players.
    ///     While > 0, only the owner can pick up the item.
    ///     Decremented each tick and becomes 0 when protection expires.
    /// </summary>
    public int ProtectionTicks { get; set; }

    /// <summary>
    ///     Timestamp when the item was dropped (for debugging/logging)
    /// </summary>
    public DateTime DroppedTime { get; set; }

    /// <summary>
    ///     Check if this item is currently protected (owned by a specific player)
    /// </summary>
    public bool IsProtected => ProtectionTicks > 0;

    /// <summary>
    ///     Check if the given player can pick up this item.
    ///     Owner can always pick up. Non-owners can only pick up if protection has expired.
    /// </summary>
    public bool CanPickUp(int playerSessionId)
    {
        if (Owner == playerSessionId)
        {
            return true;
        }

        return !IsProtected;
    }
}
