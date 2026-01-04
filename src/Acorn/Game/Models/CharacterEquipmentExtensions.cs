using System.Collections.Concurrent;
using System.Linq;
using Acorn.Database.Models;
using Acorn.Database.Repository;

namespace Acorn.Game.Models;

/// <summary>
/// Extension methods for Character equipment management.
/// Implements equip/unequip logic similar to reoserv.
/// 
/// Equipment slots use SubLoc 1-15:
/// 1 = Hat
/// 2 = Necklace
/// 3 = Armor
/// 4 = Belt
/// 5 = Boots
/// 6 = Gloves
/// 7 = Weapon
/// 8 = Shield
/// 9 = Accessory
/// 10 = Ring1
/// 11 = Ring2
/// 12 = Bracer1
/// 13 = Bracer2
/// 14 = Armlet1
/// 15 = Armlet2
/// </summary>
public static class CharacterEquipmentExtensions
{
    /// <summary>
    /// Enum for equip operation results
    /// </summary>
    public enum EquipResult
    {
        /// <summary>
        /// Item was successfully equipped
        /// </summary>
        Equipped,

        /// <summary>
        /// Item was swapped with existing equipment
        /// </summary>
        Swapped,

        /// <summary>
        /// Equip operation failed
        /// </summary>
        Failed
    }

    /// <summary>
    /// Attempt to equip an item from inventory to a specific paperdoll slot.
    /// Handles item swapping when equipping over existing equipment.
    /// </summary>
    /// <param name="character">Character attempting to equip</param>
    /// <param name="itemId">ID of item to equip</param>
    /// <param name="subLoc">Paperdoll slot (1-15) where item should be equipped</param>
    /// <param name="itemDb">Item database for retrieving item data</param>
    /// <returns>Result of equip operation</returns>
    public static EquipResult Equip(this Character character, int itemId, int subLoc, IDataFileRepository itemDb)
    {
        // Validate sub-location is a valid paperdoll slot
        if (subLoc < 1 || subLoc > 15)
            return EquipResult.Failed;

        // Get item record from database
        var itemRecord = itemDb.Eif.GetItem(itemId);
        if (itemRecord == null)
            return EquipResult.Failed;

        // Check level requirement
        if (itemRecord.LevelRequirement > 0 && character.Level < itemRecord.LevelRequirement)
            return EquipResult.Failed;

        // Check stat requirements if defined
        if (itemRecord.StrRequirement > 0 && character.Str < itemRecord.StrRequirement)
            return EquipResult.Failed;
        if (itemRecord.IntRequirement > 0 && character.Int < itemRecord.IntRequirement)
            return EquipResult.Failed;
        if (itemRecord.WisRequirement > 0 && character.Wis < itemRecord.WisRequirement)
            return EquipResult.Failed;
        if (itemRecord.AgiRequirement > 0 && character.Agi < itemRecord.AgiRequirement)
            return EquipResult.Failed;
        if (itemRecord.ConRequirement > 0 && character.Con < itemRecord.ConRequirement)
            return EquipResult.Failed;
        if (itemRecord.ChaRequirement > 0 && character.Cha < itemRecord.ChaRequirement)
            return EquipResult.Failed;

        // Check class requirement
        if (itemRecord.ClassRequirement > 0 && itemRecord.ClassRequirement != character.Class)
            return EquipResult.Failed;

        var result = EquipResult.Equipped;
        int? oldItemId = null;

        // Get current item in this slot
        var currentSlotValue = GetSlotValue(character, subLoc);

        // If there's an existing item, we'll swap it
        if (currentSlotValue != 0)
        {
            oldItemId = currentSlotValue;
            result = EquipResult.Swapped;
        }

        // Set the item in the slot
        SetSlotValue(character, subLoc, itemId);

        // Remove the item from inventory
        var inventoryItem = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (inventoryItem != null)
        {
            inventoryItem.Amount--;
            if (inventoryItem.Amount == 0)
            {
                // Remove if amount reaches 0 (rebuild bag without this item)
                character.Inventory = new Inventory(
                    [.. character.Inventory.Items.Where(i => i.Id != itemId || i.Amount > 0)]
                );
            }
        }

        // If we swapped, add old item back to inventory
        if (oldItemId.HasValue)
        {
            var existingItem = character.Inventory.Items.FirstOrDefault(i => i.Id == oldItemId.Value);
            if (existingItem != null)
            {
                existingItem.Amount++;
            }
            else
            {
                character.Inventory.Items.Add(new ItemWithAmount { Id = oldItemId.Value, Amount = 1 });
            }
        }

        return result;
    }

    /// <summary>
    /// Unequip an item from a paperdoll slot back to inventory.
    /// </summary>
    /// <param name="character">Character unequipping item</param>
    /// <param name="itemId">ID of item to unequip (for verification)</param>
    /// <param name="subLoc">Paperdoll slot (1-15) to unequip from</param>
    /// <returns>True if unequip succeeded, false otherwise</returns>
    public static bool Unequip(this Character character, int itemId, int subLoc)
    {
        // Validate sub-location
        if (subLoc < 1 || subLoc > 15)
            return false;

        // Verify the item is actually in this slot
        var slotValue = GetSlotValue(character, subLoc);
        if (slotValue != itemId)
            return false;

        // Clear the slot
        SetSlotValue(character, subLoc, 0);

        // Add item back to inventory
        var existingItem = character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            existingItem.Amount++;
        }
        else
        {
            character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = 1 });
        }

        return true;
    }

    /// <summary>
    /// Get the item ID in a paperdoll slot
    /// </summary>
    private static int GetSlotValue(Character character, int subLoc)
    {
        return subLoc switch
        {
            1 => character.Paperdoll.Hat,
            2 => character.Paperdoll.Necklace,
            3 => character.Paperdoll.Armor,
            4 => character.Paperdoll.Belt,
            5 => character.Paperdoll.Boots,
            6 => character.Paperdoll.Gloves,
            7 => character.Paperdoll.Weapon,
            8 => character.Paperdoll.Shield,
            9 => character.Paperdoll.Accessory,
            10 => character.Paperdoll.Ring1,
            11 => character.Paperdoll.Ring2,
            12 => character.Paperdoll.Bracer1,
            13 => character.Paperdoll.Bracer2,
            14 => character.Paperdoll.Armlet1,
            15 => character.Paperdoll.Armlet2,
            _ => 0
        };
    }

    /// <summary>
    /// Set the item ID in a paperdoll slot
    /// </summary>
    private static void SetSlotValue(Character character, int subLoc, int itemId)
    {
        switch (subLoc)
        {
            case 1:
                character.Paperdoll.Hat = itemId;
                break;
            case 2:
                character.Paperdoll.Necklace = itemId;
                break;
            case 3:
                character.Paperdoll.Armor = itemId;
                break;
            case 4:
                character.Paperdoll.Belt = itemId;
                break;
            case 5:
                character.Paperdoll.Boots = itemId;
                break;
            case 6:
                character.Paperdoll.Gloves = itemId;
                break;
            case 7:
                character.Paperdoll.Weapon = itemId;
                break;
            case 8:
                character.Paperdoll.Shield = itemId;
                break;
            case 9:
                character.Paperdoll.Accessory = itemId;
                break;
            case 10:
                character.Paperdoll.Ring1 = itemId;
                break;
            case 11:
                character.Paperdoll.Ring2 = itemId;
                break;
            case 12:
                character.Paperdoll.Bracer1 = itemId;
                break;
            case 13:
                character.Paperdoll.Bracer2 = itemId;
                break;
            case 14:
                character.Paperdoll.Armlet1 = itemId;
                break;
            case 15:
                character.Paperdoll.Armlet2 = itemId;
                break;
        }
    }
}
