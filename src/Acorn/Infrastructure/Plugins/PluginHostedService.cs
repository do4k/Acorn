using Acorn.Options;
using Acorn.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Instantiates the loaded plugins, raises <see cref="IAcornPlugin.OnLoadedAsync" />
///     and registers their hooks — all before the world tick and network listeners
///     start (registration order in the host guarantees this). On shutdown, plugins
///     are unloaded in reverse order after online characters have been persisted.
/// </summary>
/// <remarks>
///     Unlike discovery failures (which are fatal), a plugin that throws during
///     instantiation or <c>OnLoadedAsync</c> is disabled and the server continues
///     without it: runtime faults in one mod should not take the world down.
/// </remarks>
internal sealed class PluginHostedService(
    IServiceProvider services,
    PluginCatalog catalog,
    PluginHookDispatcher hookDispatcher,
    IConfiguration configuration,
    ILogger<PluginHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (catalog.Entries.Count == 0)
        {
            return;
        }

        var world = services.GetRequiredService<IWorldApi>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        foreach (var entry in catalog.Entries)
        {
            try
            {
                var context = new PluginContext(
                    entry.Manifest,
                    configuration.GetSection($"{PluginsOptions.OptionsSectionName}:{entry.Manifest.Id}"),
                    loggerFactory.CreateLogger($"Acorn.Plugin.{entry.Manifest.Id}"),
                    world);

                var instance = (IAcornPlugin)ActivatorUtilities.CreateInstance(services, entry.PluginType);
                await instance.OnLoadedAsync(context, cancellationToken);

                entry.Instance = instance;
                hookDispatcher.RegisterHooks(entry);

                logger.LogInformation("Loaded plugin {PluginName} {PluginVersion} (id: {PluginId}, entry: {PluginType})",
                    entry.Manifest.Name, entry.Manifest.Version, entry.Manifest.Id, entry.PluginType.Name);
            }
            catch (Exception ex)
            {
                entry.Disabled = true;
                logger.LogCritical(ex, "Plugin {PluginId} failed during load and was disabled", entry.Manifest.Id);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in catalog.Entries.Where(e => e.Instance is not null).Reverse())
        {
            try
            {
                await entry.Instance!.OnUnloadingAsync(cancellationToken);
                logger.LogInformation("Plugin {PluginId} unloaded", entry.Manifest.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin {PluginId} threw during unload", entry.Manifest.Id);
            }
        }
    }
}
