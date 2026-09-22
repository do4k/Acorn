using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     DI registration for the plugin host infrastructure.
/// </summary>
internal static class PluginServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the plugin host services for a catalog discovered at startup.
    ///     Must be called before the network/world hosted services are registered
    ///     so <see cref="PluginHostedService" /> starts (and plugins finish
    ///     loading) before any connection or tick can observe them.
    ///     <para>
    ///         Command bridging lives in <see cref="AddPluginCommands" />, which has
    ///         its own ordering requirement relative to the convention scan.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddPlugins(this IServiceCollection services, PluginCatalog catalog)
    {
        services
            .AddSingleton(catalog)
            .AddSingleton<PluginHookDispatcher>()
            .AddSingleton<IWorldApi, WorldApi>()
            .AddHostedService<PluginHostedService>();

        return services;
    }

    /// <summary>
    ///     Bridges plugin chat commands into the existing <c>#command</c>
    ///     dispatcher: one <see cref="PluginCommandAdapter" /> per plugin entry
    ///     implementing <see cref="IPluginCommand" />. Call this <b>after</b>
    ///     <c>AddAllOfType&lt;IPlayerCommandHandler&gt;()</c> so built-in commands
    ///     are matched before plugin commands (dispatch uses
    ///     <c>FirstOrDefault</c> over the handler collection).
    /// </summary>
    public static IServiceCollection AddPluginCommands(this IServiceCollection services, PluginCatalog catalog)
    {
        foreach (var entry in catalog.Entries)
        {
            if (!typeof(IPluginCommand).IsAssignableFrom(entry.PluginType))
            {
                continue;
            }

            var captured = entry;
            services.AddTransient<IPlayerCommandHandler>(sp => new PluginCommandAdapter(
                captured,
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<ILogger<PluginCommandAdapter>>()));
        }

        return services;
    }
}
