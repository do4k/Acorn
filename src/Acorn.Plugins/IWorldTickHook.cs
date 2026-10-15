namespace Acorn.Plugins;

/// <summary>
///     Raised once per world tick, after every map has ticked. World ticks are
///     serialised: the next tick only begins after this hook completes, so keep
///     implementations fast and queue any long-running work yourself.
/// </summary>
public interface IWorldTickHook : IPluginHook
{
    /// <summary>Invoked once per world tick (default 100ms).</summary>
    Task OnWorldTickAsync(WorldTickContext context);
}
