namespace Acorn.Shared.Models.Maps;

/// <summary>
/// NPC on a map.
/// </summary>
public record MapNpcRecord
{
    public int Index { get; init; }
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
    public string Direction { get; init; } = string.Empty;
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public bool IsDead { get; init; }
}

