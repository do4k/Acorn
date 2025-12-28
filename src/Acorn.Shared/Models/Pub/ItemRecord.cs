namespace Acorn.Shared.Models.Pub;

/// <summary>
/// Item record for caching in Redis.
/// </summary>
public record ItemRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int GraphicId { get; init; }
    public int Type { get; init; }
    public int SubType { get; init; }
    public int Special { get; init; }
    public int Hp { get; init; }
    public int Tp { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int Accuracy { get; init; }
    public int Evade { get; init; }
    public int Armor { get; init; }
    public int Strength { get; init; }
    public int Intelligence { get; init; }
    public int Wisdom { get; init; }
    public int Agility { get; init; }
    public int Constitution { get; init; }
    public int Charisma { get; init; }
    public int LevelRequirement { get; init; }
    public int ClassRequirement { get; init; }
    public int Weight { get; init; }
}

