namespace Acorn.Domain.Models;

/// <summary>
///     Represents a single drop entry in an NPC's loot table.
///     Uses a rate-based probability system (0-64000 range for granular control).
/// </summary>
public class LootDrop
{
    public LootDrop()
    {
    }

    public LootDrop(int itemId, int minAmount, int maxAmount, int ratePercent)
    {
        ItemId = itemId;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        RatePercent = ratePercent;
    }

    /// <summary>
    ///     ID of the item to drop
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    ///     Minimum amount to drop (0-16777215, 3-byte max)
    /// </summary>
    public int MinAmount { get; set; }

    /// <summary>
    ///     Maximum amount to drop (0-16777215, 3-byte max)
    /// </summary>
    public int MaxAmount { get; set; }

    /// <summary>
    ///     Drop rate as a percentage (0-100).
    ///     Internally converted to 0-64000 range for more granular probability calculation.
    ///     Example: 25% = 16000 chance out of 64000
    /// </summary>
    public int RatePercent { get; set; }

    /// <summary>
    ///     Converts percentage (0-100) to internal rate value (0-64000)
    /// </summary>
    public int GetInternalRate()
    {
        return RatePercent * 64000 / 100;
    }
}

/// <summary>
///     Represents the complete loot table for an NPC
/// </summary>
public class NpcLootTable
{
    /// <summary>
    ///     NPC ID this loot table belongs to
    /// </summary>
    public int NpcId { get; set; }

    /// <summary>
    ///     List of possible drops for this NPC
    /// </summary>
    public List<LootDrop> Drops { get; set; } = new();
}
