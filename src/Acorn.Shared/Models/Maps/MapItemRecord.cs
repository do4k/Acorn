namespace Acorn.Shared.Models.Maps;

/// <summary>
/// Item on a map.
/// </summary>
public record MapItemRecord
{
    public int UniqueId { get; init; }
    public int ItemId { get; init; }
    public int Amount { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
}

