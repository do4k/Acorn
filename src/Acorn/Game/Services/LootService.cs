using System.Collections.Frozen;
using Acorn.Game.Models;

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
    ///     Register drops that apply to every NPC kill, in addition to that NPC's
    ///     specific loot table (e.g. a universal gold chance). Matches reoserv's
    ///     GlobalDrops concept.
    /// </summary>
    void RegisterGlobalDrops(IEnumerable<LootDrop> drops);

    /// <summary>
    ///     Freezes loot tables for read-only hot-path use. Registration methods will
    ///     throw after this is called. Invoked once at startup after all drops load.
    /// </summary>
    void Seal();

    /// <summary>
    ///     Calculate a drop for an NPC kill (returns null if no drop occurs)
    ///     Uses probability-based random selection with weighted rates.
    ///     Considers both the NPC's specific loot table and the global drop table.
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
    private readonly List<LootDrop> _globalDrops = new();
    private FrozenDictionary<int, NpcLootTable>? _frozenNpcLootTables;

    public NpcLootTable? GetNpcLootTable(int npcId)
    {
        if (_frozenNpcLootTables is { } frozen)
        {
            return frozen.TryGetValue(npcId, out var table) ? table : null;
        }

        return _npcLootTables.TryGetValue(npcId, out var mutableTable) ? mutableTable : null;
    }

    public void RegisterNpcLootTable(NpcLootTable lootTable)
    {
        ThrowIfSealed();
        _npcLootTables[lootTable.NpcId] = lootTable;
    }

    public void RegisterGlobalDrops(IEnumerable<LootDrop> drops)
    {
        ThrowIfSealed();
        _globalDrops.AddRange(drops);
    }

    public void Seal()
    {
        if (_frozenNpcLootTables is not null)
        {
            return;
        }

        _frozenNpcLootTables = _npcLootTables.ToFrozenDictionary();
    }

    public LootDrop? RollDrop(int npcId)
    {
        var npcDrops = GetNpcLootTable(npcId)?.Drops ?? [];
        var combinedDrops = npcDrops.Concat(_globalDrops).ToList();
        if (combinedDrops.Count == 0)
        {
            return null;
        }

        // eoserv's default drop mode (DropRateMode 3): a single roll across the combined
        // chance range. When the chances add up to less than 100 the remainder is "no
        // drop"; when they exceed 100 the entries are scaled down proportionally and a
        // drop is guaranteed.
        var chanceTotal = combinedDrops.Sum(drop => drop.RatePercent);
        var roll = Random.Shared.NextDouble() * Math.Max(chanceTotal, 100.0);

        var offset = 0.0;
        foreach (var drop in combinedDrops)
        {
            if (roll >= offset && roll < offset + drop.RatePercent)
            {
                return drop;
            }

            offset += drop.RatePercent;
        }

        return null;
    }

    public int RollDropAmount(LootDrop drop)
    {
        return Random.Shared.Next(drop.MinAmount, drop.MaxAmount + 1);
    }

    private void ThrowIfSealed()
    {
        if (_frozenNpcLootTables is not null)
        {
            throw new InvalidOperationException("Loot tables have been sealed and can no longer be modified.");
        }
    }
}