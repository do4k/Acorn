using Acorn.Domain.Models;

namespace Acorn.Game.Services;

/// <summary>
///     Service for managing NPC loot tables and calculating drops
/// </summary>
public interface ILootService
{
    /// <summary>
    ///     Get the loot table for an NPC, or null if none configured
    /// </summary>
    NpcLootTable? GetNpcLootTable(int npcId);

    /// <summary>
    ///     Register or update loot table for an NPC
    /// </summary>
    void RegisterNpcLootTable(NpcLootTable lootTable);

    /// <summary>
    ///     Calculate a drop for an NPC kill (returns null if no drop occurs)
    ///     Uses probability-based random selection with weighted rates
    /// </summary>
    LootDrop? RollDrop(int npcId);

    /// <summary>
    ///     Get the amount for a drop (random between min and max)
    /// </summary>
    int RollDropAmount(LootDrop drop);
}

/// <summary>
///     Default implementation of loot service
/// </summary>
public class LootService : ILootService
{
    private readonly Dictionary<int, NpcLootTable> _npcLootTables = new();
    private readonly Random _random = new();

    public NpcLootTable? GetNpcLootTable(int npcId)
    {
        return _npcLootTables.TryGetValue(npcId, out var table) ? table : null;
    }

    public void RegisterNpcLootTable(NpcLootTable lootTable)
    {
        _npcLootTables[lootTable.NpcId] = lootTable;
    }

    public LootDrop? RollDrop(int npcId)
    {
        var lootTable = GetNpcLootTable(npcId);
        if (lootTable == null || lootTable.Drops.Count == 0)
        {
            return null;
        }

        // Sort drops by rate for proper probability weighting
        var sortedDrops = lootTable.Drops
            .OrderBy(d => d.RatePercent)
            .ToList();

        // Roll against each drop's rate
        foreach (var drop in sortedDrops)
        {
            // Generate random value from 0-64000
            var roll = _random.Next(0, 64001);
            var internalRate = drop.GetInternalRate();

            if (roll <= internalRate)
            {
                return drop;
            }
        }

        return null;
    }

    public int RollDropAmount(LootDrop drop)
    {
        return _random.Next(drop.MinAmount, drop.MaxAmount + 1);
    }
}