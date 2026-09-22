namespace Acorn.Plugins;

/// <summary>
///     Context for <see cref="IMapTickHook" />.
/// </summary>
public sealed class MapTickContext
{
    /// <summary>Id of the map that just ticked.</summary>
    public required int MapId { get; init; }

    /// <summary>Number of ticks this map has processed since server start.</summary>
    public required int TotalTicks { get; init; }

    /// <summary>Players currently on the map.</summary>
    public required int PlayerCount { get; init; }

    /// <summary>NPCs currently spawned on the map (alive and dead-awaiting-respawn).</summary>
    public required int NpcCount { get; init; }
}
