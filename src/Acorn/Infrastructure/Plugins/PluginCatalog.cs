namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     The set of plugins discovered and loaded at startup, in configured load
///     order. Immutable after discovery.
/// </summary>
internal sealed class PluginCatalog(IReadOnlyList<PluginEntry> entries)
{
    /// <summary>Empty catalog used when the plugin system is off or nothing is configured.</summary>
    public static PluginCatalog Empty { get; } = new([]);

    public IReadOnlyList<PluginEntry> Entries { get; } = entries;
}
