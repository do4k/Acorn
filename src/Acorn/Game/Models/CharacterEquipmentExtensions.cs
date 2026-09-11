using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Character = Acorn.Game.Models.Character;
using ItemWithAmount = Acorn.Game.Models.ItemWithAmount;

namespace Acorn.Game.Models;

/// <summary>
///     Extension methods for Character equipment management.
///     Implements equip/unequip logic
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

        // Check stat requirements using adjusted stats (base + class + equipment)
        // Matches reoserv equip.rs:26-33 which uses adj_strength, etc.
        if (itemRecord.StrRequirement > 0 && character.AdjStr < itemRecord.StrRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.IntRequirement > 0 && character.AdjInt < itemRecord.IntRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.WisRequirement > 0 && character.AdjWis < itemRecord.WisRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.AgiRequirement > 0 && character.AdjAgi < itemRecord.AgiRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.ConRequirement > 0 && character.AdjCon < itemRecord.ConRequirement)
        {
            return EquipResult.Failed;
        }

        if (itemRecord.ChaRequirement > 0 && character.AdjCha < itemRecord.ChaRequirement)
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
    /// <param name="itemDb">Item database used to look up the item's special flag</param>
    /// <returns>True if unequip succeeded, false otherwise</returns>
    public static bool Unequip(this Character character, int itemId, int subLoc, IDataFileRepository itemDb)
    {
        // Validate sub_loc is 0 or 1
        if (subLoc < 0 || subLoc > 1)
        {
            return false;
        }

        // Cursed equipment cannot be removed by the player. Only a CureCurse
        // item (see RemoveCursedEquipment) can take it off.
        var itemRecord = itemDb.Eif.GetItem(itemId);
        if (itemRecord?.Special == ItemSpecial.Cursed)
        {
            return false;
        }

        // Find the item by checking all slots
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
    ///     Remove every equipped item flagged as cursed. Cursed items are
    ///     destroyed rather than returned to the inventory (matching eoserv).
    /// </summary>
    /// <param name="character">Character whose cursed equipment should be removed</param>
    /// <param name="itemDb">Item database used to look up each item's special flag</param>
    /// <returns>True if at least one cursed item was removed</returns>
    public static bool RemoveCursedEquipment(this Character character, IDataFileRepository itemDb)
    {
        var removed = false;

        foreach (var (itemType, slotIndex, itemId) in EnumerateEquipped(character))
        {
            if (itemId == 0)
            {
                continue;
            }

            var itemRecord = itemDb.Eif.GetItem(itemId);
            if (itemRecord?.Special == ItemSpecial.Cursed)
            {
                ClearEquippedItem(character, itemType, slotIndex);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    ///     Enumerate every equipment slot as (item type, slot index, item id).
    /// </summary>
    private static IEnumerable<(ItemType ItemType, int SlotIndex, int ItemId)> EnumerateEquipped(Character character)
    {
        yield return (ItemType.Hat, 0, character.Paperdoll.Hat);
        yield return (ItemType.Necklace, 0, character.Paperdoll.Necklace);
        yield return (ItemType.Armor, 0, character.Paperdoll.Armor);
        yield return (ItemType.Belt, 0, character.Paperdoll.Belt);
        yield return (ItemType.Boots, 0, character.Paperdoll.Boots);
        yield return (ItemType.Gloves, 0, character.Paperdoll.Gloves);
        yield return (ItemType.Weapon, 0, character.Paperdoll.Weapon);
        yield return (ItemType.Shield, 0, character.Paperdoll.Shield);
        yield return (ItemType.Accessory, 0, character.Paperdoll.Accessory);
        yield return (ItemType.Ring, 0, character.Paperdoll.Ring1);
        yield return (ItemType.Ring, 1, character.Paperdoll.Ring2);
        yield return (ItemType.Bracer, 0, character.Paperdoll.Bracer1);
        yield return (ItemType.Bracer, 1, character.Paperdoll.Bracer2);
        yield return (ItemType.Armlet, 0, character.Paperdoll.Armlet1);
        yield return (ItemType.Armlet, 1, character.Paperdoll.Armlet2);
    }

    /// <summary>
    ///     Find where an item is equipped. Returns null if not equipped.
    /// </summary>
    private static (ItemType itemType, int slotIndex)? FindEquippedItemSlot(Character character, int itemId)
    {
        foreach (var (itemType, slotIndex, equippedItemId) in EnumerateEquipped(character))
        {
            if (equippedItemId == itemId)
            {
                return (itemType, slotIndex);
            }
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