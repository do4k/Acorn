namespace Acorn.Shared.Models.Pub;

/// <summary>
/// Spell record for caching in Redis.
/// </summary>
public record SpellRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Shout { get; init; } = string.Empty;
    public int IconId { get; init; }
    public int GraphicId { get; init; }
    public int TpCost { get; init; }
    public int SpCost { get; init; }
    public int CastTime { get; init; }
    public int Type { get; init; }
    public int TargetRestrict { get; init; }
    public int Target { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int Accuracy { get; init; }
    public int Hp { get; init; }
}

