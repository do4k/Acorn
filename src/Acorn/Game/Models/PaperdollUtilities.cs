using Acorn.Database.Models;

namespace Acorn.Game.Models;

/// <summary>
/// Utility class for managing paperdoll slot access without switch statements.
/// Provides generic getter/setter for all 15 paperdoll slots (1-indexed).
/// </summary>
public static class PaperdollUtilities
{
    private static readonly Dictionary<int, (Func<Paperdoll, int> Get, Action<Paperdoll, int> Set)> SlotMap = new()
    {
        { 1, (p => p.Hat, (p, v) => p.Hat = v) },
        { 2, (p => p.Necklace, (p, v) => p.Necklace = v) },
        { 3, (p => p.Armor, (p, v) => p.Armor = v) },
        { 4, (p => p.Belt, (p, v) => p.Belt = v) },
        { 5, (p => p.Boots, (p, v) => p.Boots = v) },
        { 6, (p => p.Gloves, (p, v) => p.Gloves = v) },
        { 7, (p => p.Weapon, (p, v) => p.Weapon = v) },
        { 8, (p => p.Shield, (p, v) => p.Shield = v) },
        { 9, (p => p.Accessory, (p, v) => p.Accessory = v) },
        { 10, (p => p.Ring1, (p, v) => p.Ring1 = v) },
        { 11, (p => p.Ring2, (p, v) => p.Ring2 = v) },
        { 12, (p => p.Bracer1, (p, v) => p.Bracer1 = v) },
        { 13, (p => p.Bracer2, (p, v) => p.Bracer2 = v) },
        { 14, (p => p.Armlet1, (p, v) => p.Armlet1 = v) },
        { 15, (p => p.Armlet2, (p, v) => p.Armlet2 = v) }
    };

    /// <summary>
    /// Gets the item ID currently equipped in the specified slot.
    /// </summary>
    /// <param name="paperdoll">The paperdoll to query</param>
    /// <param name="subLoc">The slot number (1-15)</param>
    /// <returns>The item ID in the slot, or 0 if slot is empty or invalid</returns>
    public static int GetSlotValue(Paperdoll paperdoll, int subLoc)
    {
        return SlotMap.TryGetValue(subLoc, out var slot) ? slot.Get(paperdoll) : 0;
    }

    /// <summary>
    /// Sets the item ID in the specified slot.
    /// </summary>
    /// <param name="paperdoll">The paperdoll to modify</param>
    /// <param name="subLoc">The slot number (1-15)</param>
    /// <param name="itemId">The item ID to equip (or 0 to unequip)</param>
    public static void SetSlotValue(Paperdoll paperdoll, int subLoc, int itemId)
    {
        if (SlotMap.TryGetValue(subLoc, out var slot))
        {
            slot.Set(paperdoll, itemId);
        }
    }
}
