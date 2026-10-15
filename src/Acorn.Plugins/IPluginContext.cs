using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Acorn.Plugins;

/// <summary>
///     The host services handed to a plugin during <see cref="IAcornPlugin.OnLoadedAsync" />.
/// </summary>
public interface IPluginContext
{
    /// <summary>Manifest id of the plugin.</summary>
    string PluginId { get; }

    /// <summary>Manifest version of the plugin.</summary>
    string PluginVersion { get; }

    /// <summary>
    ///     Configuration bound to this plugin's section
    ///     (<c>PluginOptions:&lt;pluginId&gt;</c> in the server configuration).
    /// </summary>
    IConfiguration Config { get; }

    /// <summary>Logger tagged with the plugin id.</summary>
    ILogger Log { get; }

    /// <summary>Curated API onto the game world.</summary>
    IWorldApi World { get; }
}
