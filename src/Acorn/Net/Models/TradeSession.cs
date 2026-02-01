using System.Collections.Concurrent;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net.Models;

/// <summary>
/// Represents an active trade session between two players
/// </summary>
public class TradeSession
{
    public required int PartnerId { get; init; }
    public required PlayerState Partner { get; init; }
    public ConcurrentBag<TradeItem> MyItems { get; private set; } = [];
    public bool IAccepted { get; set; }

    /// <summary>
    /// Max slots for trade items
    /// </summary>
    public const int MaxTradeSlots = 10;

    /// <summary>
    /// Add or update an item in the trade
    /// </summary>
    public bool AddOrUpdateItem(int itemId, int amount)
    {
        var existingItem = MyItems.FirstOrDefault(i => i.ItemId == itemId);
        if (existingItem != null)
        {
            // Update amount - need to rebuild the bag since ConcurrentBag doesn't support updates
            var newItems = new ConcurrentBag<TradeItem>(
                MyItems.Where(i => i.ItemId != itemId)
            );
            newItems.Add(new TradeItem(itemId, amount));
            MyItems = newItems;
            return true;
        }

        if (MyItems.Count >= MaxTradeSlots)
        {
            return false;
        }

        MyItems.Add(new TradeItem(itemId, amount));
        return true;
    }

    /// <summary>
    /// Remove an item from the trade
    /// </summary>
    public bool RemoveItem(int itemId)
    {
        var existingItem = MyItems.FirstOrDefault(i => i.ItemId == itemId);
        if (existingItem == null)
        {
            return false;
        }

        var newItems = new ConcurrentBag<TradeItem>(
            MyItems.Where(i => i.ItemId != itemId)
        );
        MyItems = newItems;
        return true;
    }

    /// <summary>
    /// Clear all items from the trade
    /// </summary>
    public void ClearItems()
    {
        MyItems = [];
    }

    /// <summary>
    /// Convert items to SDK Item format for packets
    /// </summary>
    public List<Item> GetItemsForPacket()
    {
        return MyItems.Select(i => new Item { Id = i.ItemId, Amount = i.Amount }).ToList();
    }
}

public record TradeItem(int ItemId, int Amount);
