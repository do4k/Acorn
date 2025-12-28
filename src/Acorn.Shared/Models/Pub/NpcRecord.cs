namespace Acorn.Shared.Models.Pub;

/// <summary>
/// NPC record for caching in Redis.
/// </summary>
public record NpcRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int GraphicId { get; init; }
    public int Type { get; init; }
    public int Hp { get; init; }
    public int Tp { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int Accuracy { get; init; }
    public int Evade { get; init; }
    public int Armor { get; init; }
    public int Experience { get; init; }
}

