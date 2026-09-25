namespace Acorn.Options;

/// <summary>
///     Configuration for the plugin (server mod) system. Plugins are loaded from
///     <see cref="Directory" /> at startup; only ids listed in <see cref="Load" />
///     are activated.
/// </summary>
public class PluginsOptions
{
    public static string SectionName => "Plugins";

    /// <summary>
    ///     Configuration section holding per-plugin options, keyed by plugin id
    ///     (<c>PluginOptions:&lt;pluginId&gt;</c>). Bound to the plugin's
    ///     <c>IPluginContext.Config</c>.
    /// </summary>
    public static string OptionsSectionName => "PluginOptions";

    /// <summary>Master switch for the plugin system.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Directory containing plugin folders. Relative paths are probed against
    ///     the server base directory, then the current working directory.
    /// </summary>
    public string Directory { get; set; } = "plugins";

    /// <summary>
    ///     Ids of plugins to load, in load order. Discovery alone never activates
    ///     a plugin; an id listed here that fails to load is a fatal startup error.
    /// </summary>
    public string[] Load { get; set; } = [];

    /// <summary>
    ///     Consecutive hook failures after which a plugin is auto-disabled for the
    ///     rest of the run.
    /// </summary>
    public int FailureThreshold { get; set; } = 50;
}
