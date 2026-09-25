namespace Acorn.Plugins;

/// <summary>
///     Versioning information for the plugin contract itself.
/// </summary>
public static class PluginContract
{
    /// <summary>
    ///     Contract version implemented by this assembly. A plugin's manifest must
    ///     declare a matching <c>contractVersion</c> or the host refuses to load it.
    ///     Bump when making breaking changes to any type in this assembly.
    /// </summary>
    public const int CurrentVersion = 1;
}
