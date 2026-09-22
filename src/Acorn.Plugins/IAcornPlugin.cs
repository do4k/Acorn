namespace Acorn.Plugins;

/// <summary>
///     Entry point for an Acorn plugin. The host expects exactly one public
///     implementation per plugin assembly; it is instantiated through dependency
///     injection at server startup.
/// </summary>
public interface IAcornPlugin
{
    /// <summary>
    ///     Called once after instantiation, before the world tick and network
    ///     listeners start. Store <paramref name="context" /> for later use.
    /// </summary>
    Task OnLoadedAsync(IPluginContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Called once during graceful server shutdown, after online characters
    ///     have been persisted. Use it to flush plugin-owned state.
    /// </summary>
    Task OnUnloadingAsync(CancellationToken cancellationToken);
}
