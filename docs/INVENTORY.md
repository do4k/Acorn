# Inventory System

## Overview

Acorn uses a **serialized string-based inventory system** that stores player inventory and bank data as delimited strings in the database. This approach is consistent with EOSERV and reoserv implementations for legacy compatibility.

## Database Schema

### Storage Format

- **Inventory**: `TEXT/NVARCHAR(2000)` field in the `Characters` table
- **Bank**: `TEXT/NVARCHAR(2000)` field in the `Characters` table
- **Format**: `itemId,amount|itemId,amount|itemId,amount`
- **Example**: `"1,5|23,10|45,1"` = 5x item ID 1, 10x item ID 23, 1x item ID 45

### Capacity Limits

- **Inventory**: Limited by 2000 character string (approximately 285-400 slots depending on item IDs)
- **Bank**: Limited by `BankMax` field (default varies, plus 2000 char serialization limit)
- **Weight**: Limited by `MaxWeight` character stat

## Runtime Representation

### Game Model

```csharp
// Inventory is stored in memory as a ConcurrentBag for thread-safety
public record Inventory(ConcurrentBag<ItemWithAmount> Items);
public record Bank(ConcurrentBag<ItemWithAmount> Items);

// Item structure
public class ItemWithAmount
{
    public int Id { get; set; }
    public int Amount { get; set; }
}
```

### Serialization/Deserialization

**To Database** (in `Character.AsDatabaseModel()`):
```csharp
Inventory = string.Join("|", Inventory.Items.Select(i => $"{i.Id},{i.Amount}"))
```

**From Database** (in `Character.AsGameModel()`):
```csharp
Inventory = new Inventory(GetItemsWithAmount(Inventory))

// GetItemsWithAmount helper:
s.Split('|').Select(b => {
    var parts = b.Split(',');
    return new ItemWithAmount { Id = int.Parse(parts[0]), Amount = int.Parse(parts[1]) };
})
```

## API Methods

### Inventory Operations

#### `AddItem(int itemId, int amount = 1) -> bool`
Adds items to inventory. Automatically stacks with existing items.
```csharp
if (player.Character.AddItem(itemId: 1, amount: 5))
{
    // Successfully added 5x item ID 1
}
```

#### `RemoveItem(int itemId, int amount = 1) -> bool`
Removes items from inventory. Returns false if player doesn't have enough.
```csharp
if (player.Character.RemoveItem(itemId: 1, amount: 3))
{
    // Successfully removed 3x item ID 1
}
```

#### `HasItem(int itemId, int amount = 1) -> bool`
Checks if player has specified item and quantity.
```csharp
if (player.Character.HasItem(itemId: 1, amount: 5))
{
    // Player has at least 5x item ID 1
}
```

#### `GetItemAmount(int itemId) -> int`
Returns the total amount of a specific item.
```csharp
int goldAmount = player.Character.GetItemAmount(itemId: 1);
```

#### `GetInventorySlotCount() -> int`
Returns number of occupied inventory slots.
```csharp
int usedSlots = player.Character.GetInventorySlotCount();
```

### Weight Management

#### `GetCurrentWeight(Eif items) -> int`
Calculates total weight of all items in inventory.
```csharp
int currentWeight = player.Character.GetCurrentWeight(eif);
if (currentWeight >= player.Character.MaxWeight)
{
    // Player is encumbered
}
```

#### `CanCarryWeight(Eif items, int itemId, int amount = 1) -> bool`
Checks if adding an item would exceed weight limit.
```csharp
if (player.Character.CanCarryWeight(eif, itemId: 100, amount: 5))
{
    // Player can carry 5x item ID 100
    player.Character.AddItem(itemId: 100, amount: 5);
}
```

### Bank Operations

#### `AddBankItem(int itemId, int amount = 1) -> bool`
Adds items to bank. Respects `BankMax` capacity limit.
```csharp
if (player.Character.AddBankItem(itemId: 1, amount: 100))
{
    // Successfully deposited 100x item ID 1
}
```

#### `RemoveBankItem(int itemId, int amount = 1) -> bool`
Removes items from bank.
```csharp
if (player.Character.RemoveBankItem(itemId: 1, amount: 50))
{
    // Successfully withdrew 50x item ID 1
}
```

#### `HasBankItem(int itemId, int amount = 1) -> bool`
Checks if player has specified item in bank.
```csharp
if (player.Character.HasBankItem(itemId: 1, amount: 100))
{
    // Player has at least 100x item ID 1 in bank
}
```

## Usage Examples

### Item Drop Handler

```csharp
public async Task HandleAsync(PlayerState player, ItemDropClientPacket packet)
{
    // Validate player has the item
    if (!player.Character.HasItem(packet.Item.Id, packet.Item.Amount))
    {
        return; // Player doesn't have the item
    }

    // Validate drop location is valid
    var distance = Math.Abs(packet.Coords.X - player.Character.X) + 
                   Math.Abs(packet.Coords.Y - player.Character.Y);
    if (distance > 1)
    {
        return; // Too far away
    }

    // Remove from inventory
    if (player.Character.RemoveItem(packet.Item.Id, packet.Item.Amount))
    {
        // Add item to map
        // Broadcast to nearby players
        // Send updated inventory to player
    }
}
```

### Shop Buy Handler

```csharp
public async Task HandleAsync(PlayerState player, ShopBuyClientPacket packet)
{
    const int GOLD_ITEM_ID = 1;
    
    // Get item data from EIF
    var itemData = eif.GetItem(packet.BuyItem.Id);
    int totalCost = itemData.Price * packet.BuyItem.Amount;
    
    // Check if player has enough gold
    if (!player.Character.HasItem(GOLD_ITEM_ID, totalCost))
    {
        return; // Not enough gold
    }
    
    // Check weight limit
    if (!player.Character.CanCarryWeight(eif, packet.BuyItem.Id, packet.BuyItem.Amount))
    {
        return; // Too heavy
    }
    
    // Process transaction
    if (player.Character.RemoveItem(GOLD_ITEM_ID, totalCost) &&
        player.Character.AddItem(packet.BuyItem.Id, packet.BuyItem.Amount))
    {
        // Send updated inventory
        // Update database
    }
}
```

### Item Junk Handler

```csharp
public async Task HandleAsync(PlayerState player, ItemJunkClientPacket packet)
{
    // Validate and remove (destroys the item)
    if (player.Character.RemoveItem(packet.Item.Id, packet.Item.Amount))
    {
        // Send ItemJunk packet with updated inventory
        // Update character in database
    }
}
```

## Important Notes

### Thread Safety
- Uses `ConcurrentBag<ItemWithAmount>` for thread-safe operations
- Safe for concurrent access from multiple async handlers

### Item Stacking
- All items stack automatically - no separate stack logic needed
- Amount is stored per unique item ID
- No maximum stack size enforced by default

### Database Persistence
- Changes to inventory are **in-memory only** until saved
- Must call repository save methods to persist changes:
  ```csharp
  await characterRepository.UpdateAsync(player.Character.AsDatabaseModel());
  ```

### Empty Stacks
- Zero-amount items are automatically removed during `RemoveItem()`
- ConcurrentBag reconstruction used since it doesn't support direct removal

### Weight System
- Requires EIF (item data file) to calculate weights
- Each item in EIF has a `Weight` property
- Total weight = sum of (item.Weight * item.Amount)

### Gold Handling
- Gold is typically item ID 1 (verify in your EIF)
- Treated as a regular inventory item
- Bank gold is stored separately in `GoldBank` field (integer)

## Future Enhancements

Potential improvements for the inventory system:

1. **Max Stack Sizes**: Add per-item stack limits from EIF
2. **Inventory Slots**: Enforce maximum slot count based on character stats
3. **Item Types**: Special handling for quest items, equipment, etc.
4. **Trade-Locked Items**: Prevent trading certain items
5. **Item Uniqueness**: Enforce one-of-a-kind items (wedding rings, etc.)
6. **Event Hooks**: Fire events on inventory changes for UI updates
7. **Async Persistence**: Auto-save inventory changes with debouncing
8. **Item Expiration**: Support for temporary items with expiration dates
