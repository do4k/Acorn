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
