namespace Acorn.Game.Models;

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
