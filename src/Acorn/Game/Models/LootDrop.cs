namespace Acorn.Game.Models;

/// <summary>
///     Represents a single drop entry in an NPC's loot table.
///     Chances are percentages (0-100) and match eoserv's drop configuration, which
///     allows fractional values such as <c>0.5</c> for half a percent.
/// </summary>
public class LootDrop
{
    public LootDrop()
    {
    }

    public LootDrop(int itemId, int minAmount, int maxAmount, double ratePercent)
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
    ///     Drop chance as a percentage (0-100). Example: 25 means 25%.
    /// </summary>
    public double RatePercent { get; set; }
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
