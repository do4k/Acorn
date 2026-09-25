using System.Reflection;
using System.Text.Json;
using Acorn.Options;
using Acorn.Plugins;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Discovers and loads the plugins enabled in configuration, before the host
///     is built, so plugin command bridges can participate in DI.
/// </summary>
/// <remarks>
///     Layout: <c>{Directory}/{pluginId}/plugin.json</c> plus the entry assembly
///     and its private dependencies. Only ids listed in <see cref="PluginsOptions.Load" />
///     are ever loaded — discovery alone never activates a plugin. Failures on an
///     explicitly enabled plugin throw <see cref="PluginLoadException" /> (fail fast).
/// </remarks>
internal static class PluginDiscovery
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PluginCatalog Discover(PluginsOptions options)
    {
        if (!options.Enabled)
        {
            return PluginCatalog.Empty;
        }

        var requested = options.Load
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requested.Count == 0)
        {
            return PluginCatalog.Empty;
        }

        var root = ResolvePluginsDirectory(options.Directory);
        var entries = requested.Select(id => LoadPlugin(root, id)).ToList();
        return new PluginCatalog(entries);
    }

    private static PluginEntry LoadPlugin(string root, string id)
    {
        var directory = Path.GetFullPath(Path.Combine(root, id));
        if (!Directory.Exists(directory))
        {
            throw new PluginLoadException($"Plugin '{id}': no plugin directory at '{directory}'.");
        }

        var manifest = ReadManifest(directory, id);
        ValidateContract(id, manifest);

        var entryPath = ValidateEntryAssemblyPath(directory, id, manifest);

        PluginLoadContext loadContext = new(entryPath);
        Assembly assembly;
        Type[] pluginTypes;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(entryPath);
            pluginTypes = assembly.GetExportedTypes()
                .Where(t => typeof(IAcornPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                .ToArray();
        }
        catch (Exception ex) when (ex is not PluginLoadException)
        {
            throw new PluginLoadException(
                $"Plugin '{id}': failed to load entry assembly '{manifest.EntryAssembly}': {ex.Message}", ex);
        }

        if (pluginTypes.Length != 1)
        {
            throw new PluginLoadException(
                $"Plugin '{id}': expected exactly one public IAcornPlugin implementation in '{manifest.EntryAssembly}', found {pluginTypes.Length}.");
        }

        return new PluginEntry(manifest, assembly, pluginTypes[0], loadContext);
    }

    private static PluginManifest ReadManifest(string directory, string id)
    {
        var manifestPath = Path.Combine(directory, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            throw new PluginLoadException($"Plugin '{id}': missing manifest '{manifestPath}'.");
        }

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PluginLoadException($"Plugin '{id}': invalid manifest: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new PluginLoadException($"Plugin '{id}': manifest is empty.");
        }

        if (!string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginLoadException(
                $"Plugin '{id}': manifest id '{manifest.Id}' does not match the plugin directory name.");
        }

        return manifest;
    }

    private static void ValidateContract(string id, PluginManifest manifest)
    {
        if (manifest.ContractVersion != PluginContract.CurrentVersion)
        {
            throw new PluginLoadException(
                $"Plugin '{id}': built against plugin contract v{manifest.ContractVersion}, but this server supports v{PluginContract.CurrentVersion}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.MinHostVersion))
        {
            return;
        }

        if (!Version.TryParse(manifest.MinHostVersion, out var minHostVersion))
        {
            throw new PluginLoadException($"Plugin '{id}': invalid minHostVersion '{manifest.MinHostVersion}'.");
        }

        var hostVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (hostVersion is not null && hostVersion < minHostVersion)
        {
            throw new PluginLoadException(
                $"Plugin '{id}': requires server version {minHostVersion} or newer, but this server is {hostVersion}.");
        }
    }

    /// <summary>
        /// Resolves the manifest's entry assembly to a full path, rejecting rooted
        /// paths and traversal outside the plugin directory (a manifest must never
        /// turn the loader into an arbitrary-code-execution vector).
    /// </summary>
    private static string ValidateEntryAssemblyPath(string directory, string id, PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly) ||
            Path.IsPathRooted(manifest.EntryAssembly))
        {
            throw new PluginLoadException(
                $"Plugin '{id}': entryAssembly must be a relative file name inside the plugin directory.");
        }

        var entryPath = Path.GetFullPath(Path.Combine(directory, manifest.EntryAssembly));
        if (!entryPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new PluginLoadException(
                $"Plugin '{id}': entryAssembly resolves outside the plugin directory.");
        }

        if (!File.Exists(entryPath))
        {
            throw new PluginLoadException(
                $"Plugin '{id}': entry assembly '{manifest.EntryAssembly}' not found in '{directory}'.");
        }

        return entryPath;
    }

    private static string ResolvePluginsDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new PluginLoadException("Plugins:Directory must not be empty when plugins are configured to load.");
        }

        if (Path.IsPathRooted(directory))
        {
            if (!Directory.Exists(directory))
            {
                throw new PluginLoadException($"Configured plugins directory '{directory}' does not exist.");
            }

            return Path.GetFullPath(directory);
        }

        // Relative directories are probed against the server base directory first
        // (deployed layout) and then the working directory (dotnet run from the repo).
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, directory)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), directory))
        };

        foreach (var candidate in candidates.Where(Directory.Exists))
        {
            return candidate;
        }

        throw new PluginLoadException(
            $"Plugins are configured to load but directory '{directory}' was not found (probed: {string.Join(", ", candidates)}).");
    }
}
