using System.Reflection;
using Acorn.Plugins;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     A discovered plugin: its manifest, the assembly loaded into its own
///     <see cref="PluginLoadContext" />, and runtime bookkeeping (instance,
///     health). Mutable health state (<see cref="ConsecutiveHookFailures" />,
///     <see cref="Disabled" />) is accessed from parallel map tick tasks and
///     must be thread-safe.
/// </summary>
internal sealed class PluginEntry(
    PluginManifest manifest,
    Assembly assembly,
    Type pluginType,
    PluginLoadContext? loadContext)
{
    private volatile bool _disabled;
    private int _consecutiveHookFailures;

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
    ///     Volatile so parallel map tick tasks observe the flag immediately.
    /// </summary>
    public bool Disabled
    {
        get => _disabled;
        set => _disabled = value;
    }

    /// <summary>
    ///     Consecutive hook failures, reset on the first success. Thread-safe:
    ///     map ticks run in parallel via <c>Task.WhenAll</c> so concurrent hooks
    ///     may increment/reset this simultaneously.
    /// </summary>
    public int ConsecutiveHookFailures
    {
        get => Volatile.Read(ref _consecutiveHookFailures);
        set => Interlocked.Exchange(ref _consecutiveHookFailures, value);
    }

    /// <summary>Atomically increments the failure counter and returns the new value.</summary>
    public int IncrementFailures() => Interlocked.Increment(ref _consecutiveHookFailures);

    /// <summary>Atomically resets the failure counter to zero.</summary>
    public void ResetFailures() => Interlocked.Exchange(ref _consecutiveHookFailures, 0);
}
