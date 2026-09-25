using Acorn.Options;
using Acorn.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Collects the hook interfaces implemented by each plugin's entry instance at
///     startup and dispatches hook events to them inline at the host call sites.
/// </summary>
/// <remarks>
///     <para>
///         Every invocation is isolated: a throwing plugin is logged and counted,
///         never allowed to break the world tick or a connection. After
///         <see cref="PluginsOptions.FailureThreshold" /> consecutive failures the
///         plugin is auto-disabled for the rest of the run.
///     </para>
///     <para>
///         Registration completes during <see cref="PluginHostedService" /> startup,
///         before the world tick and listeners run, so the hook lists are only ever
///         read during dispatch. Hooks may run concurrently with each other (maps
///         tick in parallel) and with packet handling.
///     </para>
/// </remarks>
internal sealed class PluginHookDispatcher(
    IOptions<PluginsOptions> options,
    ILogger<PluginHookDispatcher> logger)
{
    private readonly int _failureThreshold = Math.Max(1, options.Value.FailureThreshold);

    private List<HookRegistration> _mapTickHooks = [];
    private List<HookRegistration> _worldTickHooks = [];

    /// <summary>Fast guard so the host skips building contexts when no plugin listens.</summary>
    public bool HasWorldTickHooks => _worldTickHooks.Count > 0;

    /// <summary>Fast guard so the host skips building contexts when no plugin listens.</summary>
    public bool HasMapTickHooks => _mapTickHooks.Count > 0;

    /// <summary>
    ///     Collects every hook interface implemented by the plugin's entry
    ///     instance, ordered by <see cref="IPluginHook.Priority" /> (stable).
    /// </summary>
    public void RegisterHooks(PluginEntry entry)
    {
        if (entry.Instance is IWorldTickHook worldTickHook)
        {
            _worldTickHooks = [.. _worldTickHooks.Append(new HookRegistration(entry, worldTickHook))
                .OrderBy(h => h.Hook.Priority)];
        }

        if (entry.Instance is IMapTickHook mapTickHook)
        {
            _mapTickHooks = [.. _mapTickHooks.Append(new HookRegistration(entry, mapTickHook))
                .OrderBy(h => h.Hook.Priority)];
        }
    }

    public Task RaiseWorldTickAsync(WorldTickContext context)
    {
        return RaiseAsync(_worldTickHooks, context,
            static (hook, ctx) => ((IWorldTickHook)hook).OnWorldTickAsync(ctx));
    }

    public Task RaiseMapTickAsync(MapTickContext context)
    {
        return RaiseAsync(_mapTickHooks, context,
            static (hook, ctx) => ((IMapTickHook)hook).OnMapTickAsync(ctx));
    }

    private async Task RaiseAsync<TContext>(
        IReadOnlyList<HookRegistration> hooks,
        TContext context,
        Func<IPluginHook, TContext, Task> invoke)
    {
        foreach (var (entry, hook) in hooks)
        {
            if (entry.Disabled)
            {
                continue;
            }

            try
            {
                await invoke(hook, context);
                entry.ResetFailures();
            }
            catch (Exception ex)
            {
                var failures = entry.IncrementFailures();
                logger.LogError(ex, "Plugin {PluginId} hook {HookType} failed ({Failures} consecutive)",
                    entry.Manifest.Id, hook.GetType().Name, failures);

                if (failures >= _failureThreshold)
                {
                    entry.Disabled = true;
                    logger.LogCritical(
                        "Plugin {PluginId} auto-disabled for this run after {Failures} consecutive hook failures",
                        entry.Manifest.Id, failures);
                }
            }
        }
    }

    private sealed record HookRegistration(PluginEntry Entry, IPluginHook Hook);
}
