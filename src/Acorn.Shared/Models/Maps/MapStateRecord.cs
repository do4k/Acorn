namespace Acorn.Shared.Models.Maps;

/// <summary>
/// Cached map state for API consumption.
/// </summary>
public record MapStateRecord
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public int PlayerCount { get; init; }
    public int NpcCount { get; init; }
    public int ItemCount { get; init; }
    public IReadOnlyList<MapPlayerRecord> Players { get; init; } = [];
    public IReadOnlyList<MapNpcRecord> Npcs { get; init; } = [];
    public IReadOnlyList<MapItemRecord> Items { get; init; } = [];
}

