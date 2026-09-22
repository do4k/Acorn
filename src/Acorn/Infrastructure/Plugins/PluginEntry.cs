using System.Reflection;
using Acorn.Plugins;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     A discovered plugin: its manifest, the assembly loaded into its own
///     <see cref="PluginLoadContext" />, and runtime bookkeeping (instance,
///     health). Mutable state is only touched from the hosted-service and hook
///     dispatch paths.
/// </summary>
internal sealed class PluginEntry(
    PluginManifest manifest,
    Assembly assembly,
    Type pluginType,
    PluginLoadContext? loadContext)
{
    public PluginManifest Manifest { get; } = manifest;

    public Assembly Assembly { get; } = assembly;

    /// <summary>The plugin's single public <see cref="IAcornPlugin" /> implementation.</summary>
    public Type PluginType { get; } = pluginType;

    /// <summary>The isolated load context holding the plugin assembly (null in tests).</summary>
    public PluginLoadContext? LoadContext { get; } = loadContext;

    /// <summary>Instantiated plugin entry point; set after a successful load.</summary>
    public IAcornPlugin? Instance { get; set; }

    /// <summary>
    ///     When true the plugin is skipped everywhere: hooks, commands and lifecycle.
    ///     Set on load failure or after repeated hook failures (auto-disable).
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>Consecutive hook failures, reset on the first success.</summary>
    public int ConsecutiveHookFailures { get; set; }
}
