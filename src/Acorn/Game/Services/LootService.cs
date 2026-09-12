using System.Collections.Frozen;
using Acorn.Game.Models;

namespace Acorn.Game.Services;

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