using System.Text.Json.Serialization;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Contents of a plugin's <c>plugin.json</c> manifest.
/// </summary>
internal sealed class PluginManifest
{
    /// <summary>Stable plugin id; must match the plugin's directory name.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Human-readable plugin name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Plugin version (informational; not semver-validated).</summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    ///     Entry assembly file name, relative to the plugin directory. Must stay
    ///     inside the plugin directory.
    /// </summary>
    [JsonPropertyName("entryAssembly")]
    public required string EntryAssembly { get; init; }

    /// <summary>
    ///     Version of <c>Acorn.Plugins</c> the plugin was built against. Must equal
    ///     <see cref="Acorn.Plugins.PluginContract.CurrentVersion" />.
    /// </summary>
    [JsonPropertyName("contractVersion")]
    public required int ContractVersion { get; init; }

    /// <summary>Optional minimum Acorn server version required by the plugin.</summary>
    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; init; }
}
