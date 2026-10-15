namespace Acorn.Plugins;

/// <summary>
///     Raised once per map per world tick, after the map's own tick processing
///     (NPC actions, recovery, doors, cleanup). Map ticks for different maps may
///     run concurrently with each other and with packet handling, so hook state
///     must be thread-safe.
/// </summary>
public interface IMapTickHook : IPluginHook
{
    /// <summary>Invoked once per map per world tick.</summary>
    Task OnMapTickAsync(MapTickContext context);
}
