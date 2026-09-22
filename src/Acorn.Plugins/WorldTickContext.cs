namespace Acorn.Plugins;

/// <summary>
///     Context for <see cref="IWorldTickHook" />.
/// </summary>
public sealed class WorldTickContext
{
    /// <summary>Monotonic world tick counter since server start.</summary>
    public required long TotalTicks { get; init; }

    /// <summary>Number of players connected when the tick completed.</summary>
    public required int OnlinePlayerCount { get; init; }
}
