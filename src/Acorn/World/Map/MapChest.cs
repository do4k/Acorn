using System.Collections.Concurrent;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Map;

/// <summary>
/// Represents a chest on the map
/// </summary>
public class MapChest
{
    public required Coords Coords { get; init; }
    public int? RequiredKeyId { get; init; }
    public ConcurrentBag<ChestItem> Items { get; set; } = [];
    public int MaxSlots { get; init; } = 10;
}
