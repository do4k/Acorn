namespace Acorn.Plugins;

/// <summary>
///     Marker interface for plugin hooks. The host collects every hook interface
///     implemented by a plugin's <see cref="IAcornPlugin" /> instance at startup.
/// </summary>
public interface IPluginHook
{
    /// <summary>
    ///     Dispatch order within a hook event: lower values run first. The default
    ///     is 0; use negative values to run before other plugins and positive to
    ///     run after.
    /// </summary>
    int Priority => 0;
}
