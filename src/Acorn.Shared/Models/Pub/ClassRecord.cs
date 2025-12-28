namespace Acorn.Shared.Models.Pub;

/// <summary>
/// Class record for caching in Redis.
/// </summary>
public record ClassRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ParentType { get; init; }
    public int StatGroup { get; init; }
    public int Strength { get; init; }
    public int Intelligence { get; init; }
    public int Wisdom { get; init; }
    public int Agility { get; init; }
    public int Constitution { get; init; }
    public int Charisma { get; init; }
}

