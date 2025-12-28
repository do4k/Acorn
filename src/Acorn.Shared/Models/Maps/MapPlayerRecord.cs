namespace Acorn.Shared.Models.Maps;

/// <summary>
/// Player on a map.
/// </summary>
public record MapPlayerRecord
{
    public int SessionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
    public string Direction { get; init; } = string.Empty;
    public int Level { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public string SitState { get; init; } = string.Empty;
    public bool Hidden { get; init; }
}

