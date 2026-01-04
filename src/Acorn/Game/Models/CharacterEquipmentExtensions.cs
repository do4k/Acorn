using Acorn.Database.Models;
using Acorn.Database.Repository;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Models;

/// <summary>
///     Extension methods for Character equipment management.
///     Implements equip/unequip logic matching reoserv implementation.
///     NOTE: subLoc is NOT a slot number. It's an array index for multi-slot items:
///     - For single-slot items (Weapon, Shield, Armor, Hat, Boots, Gloves, Accessory, Belt, Necklace):
///     subLoc must be 0 and is ignored
///     - For multi-slot items (Ring, Armlet, Bracer): subLoc is 0 or 1 (array index)
/// </summary>
public static class CharacterEquipmentExtensions
{
    /// <summary>
    ///     Enum for equip operation results
    /// </summary>
    public enum EquipResult
    {
        Equipped,
        Swapped,
        Failed
    }

    /// <summary>
    ///     Attempt to equip an item from inventory.
    ///     The target slot is determined by the item's type from the database.
    /// </summary>
    /// <param name="character">Character attempting to equip</param>
    /// <param name="itemId">ID of item to equip</param>
    /// <param name="subLoc">Array index for multi-slot items (Ring, Armlet, Bracer), must be 0 for single-slot items</param>
    /// <param name="itemDb">Item database for retrieving item data</param>
    /// <returns>Result of equip operation</returns>
    public static EquipResult Equip(this Character character, int itemId, int subLoc, IDataFileRepository itemDb)
    {
        // Validate sub_loc is 0 or 1 (array index for multi-slot items)
        if (subLoc < 0 || subLoc > 1)
        {
            return EquipResult.Failed;
        }

        // Get item record from database
        var itemRecord = itemDb.Eif.GetItem(itemId);
        if (itemRecord == null)
        {
            return EquipResult.Failed;
        }

        // Check level requirement
        if (itemRecord.LevelRequirement > 0 && character.Level < itemRecord.LevelRequirement)
        {
            return EquipResult.Failed;
        }

        // Check stat requirements if defined
        if (itemRecord.StrRequirement > 0 && character.Str < itemRecord.StrRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.IntRequirement > 0 && character.Int < itemRecord.IntRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.WisRequirement > 0 && character.Wis < itemRecord.WisRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.AgiRequirement > 0 && character.Agi < itemRecord.AgiRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.ConRequirement > 0 && character.Con < itemRecord.ConRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.ChaRequirement > 0 && character.Cha < itemRecord.ChaRequirement)
        {
            return EquipResult.Failed;
        }

        // Check class requirement
        if (itemRecord.ClassRequirement > 0 && itemRecord.ClassRequirement != character.Class)
        {
            return EquipResult.Failed;
        }

        var result = EquipResult.Equipped;
        int? oldItemId = null;

        // Get the appropriate equipment slot based on item type
        var currentItemId = GetEquippedItem(character, itemRecord.Type, subLoc);

        // If there's an existing item, we'll swap it
        if (currentItemId != 0)
        {
            oldItemId = currentItemId;
            result = EquipResult.Swapped;
        }

        // Set the item in the appropriate slot
        SetEquippedItem(character, itemRecord.Type, subLoc, itemId);

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
    ///     Unequip an item from paperdoll back to inventory.
    /// </summary>
    /// <param name="character">Character unequipping item</param>
    /// <param name="itemId">ID of item to unequip (for verification)</param>
    /// <param name="subLoc">Array index for multi-slot items, ignored for single-slot items</param>
    /// <returns>True if unequip succeeded, false otherwise</returns>
    public static bool Unequip(this Character character, int itemId, int subLoc)
    {
        // Validate sub_loc is 0 or 1
        if (subLoc < 0 || subLoc > 1)
        {
            return false;
        }

        // We need the item database to determine the item type
        // For now, we'll find the item by checking all slots
        // This is a fallback approach - ideally we'd have access to itemDb here
        var foundSlot = FindEquippedItemSlot(character, itemId);
        if (foundSlot == null)
        {
            return false;
        }

        // Clear the slot
        ClearEquippedItem(character, foundSlot.Value.itemType, foundSlot.Value.slotIndex);

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
    ///     Find where an item is equipped. Returns null if not equipped.
    /// </summary>
    private static (ItemType itemType, int slotIndex)? FindEquippedItemSlot(Character character, int itemId)
    {
        if (character.Paperdoll.Hat == itemId)
        {
            return (ItemType.Hat, 0);
        }

        if (character.Paperdoll.Necklace == itemId)
        {
            return (ItemType.Necklace, 0);
        }

        if (character.Paperdoll.Armor == itemId)
        {
            return (ItemType.Armor, 0);
        }

        if (character.Paperdoll.Belt == itemId)
        {
            return (ItemType.Belt, 0);
        }

        if (character.Paperdoll.Boots == itemId)
        {
            return (ItemType.Boots, 0);
        }

        if (character.Paperdoll.Gloves == itemId)
        {
            return (ItemType.Gloves, 0);
        }

        if (character.Paperdoll.Weapon == itemId)
        {
            return (ItemType.Weapon, 0);
        }

        if (character.Paperdoll.Shield == itemId)
        {
            return (ItemType.Shield, 0);
        }

        if (character.Paperdoll.Accessory == itemId)
        {
            return (ItemType.Accessory, 0);
        }

        if (character.Paperdoll.Ring1 == itemId)
        {
            return (ItemType.Ring, 0);
        }

        if (character.Paperdoll.Ring2 == itemId)
        {
            return (ItemType.Ring, 1);
        }

        if (character.Paperdoll.Bracer1 == itemId)
        {
            return (ItemType.Bracer, 0);
        }

        if (character.Paperdoll.Bracer2 == itemId)
        {
            return (ItemType.Bracer, 1);
        }

        if (character.Paperdoll.Armlet1 == itemId)
        {
            return (ItemType.Armlet, 0);
        }

        if (character.Paperdoll.Armlet2 == itemId)
        {
            return (ItemType.Armlet, 1);
        }

        return null;
    }

    /// <summary>
    ///     Get the item ID currently equipped in a slot.
    /// </summary>
    private static int GetEquippedItem(Character character, ItemType itemType, int slotIndex)
    {
        return itemType switch
        {
            ItemType.Weapon => character.Paperdoll.Weapon,
            ItemType.Shield => character.Paperdoll.Shield,
            ItemType.Armor => character.Paperdoll.Armor,
            ItemType.Hat => character.Paperdoll.Hat,
            ItemType.Boots => character.Paperdoll.Boots,
            ItemType.Gloves => character.Paperdoll.Gloves,
            ItemType.Accessory => character.Paperdoll.Accessory,
            ItemType.Belt => character.Paperdoll.Belt,
            ItemType.Necklace => character.Paperdoll.Necklace,
            ItemType.Ring => slotIndex == 0 ? character.Paperdoll.Ring1 : character.Paperdoll.Ring2,
            ItemType.Armlet => slotIndex == 0 ? character.Paperdoll.Armlet1 : character.Paperdoll.Armlet2,
            ItemType.Bracer => slotIndex == 0 ? character.Paperdoll.Bracer1 : character.Paperdoll.Bracer2,
            _ => 0
        };
    }

    /// <summary>
    ///     Set the item ID in an equipment slot.
    /// </summary>
    private static void SetEquippedItem(Character character, ItemType itemType, int slotIndex, int itemId)
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                character.Paperdoll.Weapon = itemId;
                break;
            case ItemType.Shield:
                character.Paperdoll.Shield = itemId;
                break;
            case ItemType.Armor:
                character.Paperdoll.Armor = itemId;
                break;
            case ItemType.Hat:
                character.Paperdoll.Hat = itemId;
                break;
            case ItemType.Boots:
                character.Paperdoll.Boots = itemId;
                break;
            case ItemType.Gloves:
                character.Paperdoll.Gloves = itemId;
                break;
            case ItemType.Accessory:
                character.Paperdoll.Accessory = itemId;
                break;
            case ItemType.Belt:
                character.Paperdoll.Belt = itemId;
                break;
            case ItemType.Necklace:
                character.Paperdoll.Necklace = itemId;
                break;
            case ItemType.Ring:
                if (slotIndex == 0)
                {
                    character.Paperdoll.Ring1 = itemId;
                }
                else
                {
                    character.Paperdoll.Ring2 = itemId;
                }

                break;
            case ItemType.Armlet:
                if (slotIndex == 0)
                {
                    character.Paperdoll.Armlet1 = itemId;
                }
                else
                {
                    character.Paperdoll.Armlet2 = itemId;
                }

                break;
            case ItemType.Bracer:
                if (slotIndex == 0)
                {
                    character.Paperdoll.Bracer1 = itemId;
                }
                else
                {
                    character.Paperdoll.Bracer2 = itemId;
                }

                break;
        }
    }

    /// <summary>
    ///     Clear an equipment slot (set to 0).
    /// </summary>
    private static void ClearEquippedItem(Character character, ItemType itemType, int slotIndex)
    {
        SetEquippedItem(character, itemType, slotIndex, 0);
    }
}