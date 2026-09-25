using System.Reflection;
using System.Runtime.Loader;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Per-plugin <see cref="AssemblyLoadContext" />. Gives each plugin isolation
///     for its private dependencies (two plugins may carry conflicting versions of
///     the same package) while unifying type identity for the assemblies that
///     cross the plugin boundary: the contracts assembly, the EO protocol SDK and
///     the Microsoft.Extensions abstractions. Those resolve to the host's copies
///     by returning <c>null</c> here and falling through to the default context.
/// </summary>
/// <remarks>
///     Not collectible: Phase 0 plugins are startup-loaded and live for the
///     process lifetime (hot reload is a later phase).
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.Ordinal)
    {
        "Acorn.Plugins",
        "Moffat.EndlessOnline.SDK"
    };

    private readonly AssemblyDependencyResolver? _resolver;

    public PluginLoadContext(string entryAssemblyPath)
        : base($"AcornPlugin:{Path.GetFileNameWithoutExtension(entryAssemblyPath)}", isCollectible: false)
        // TODO: Phase 3 hot reload requires isCollectible: true, which constrains
        // plugin code (no static state that roots the ALC, weak references for
        // callbacks, etc.). Leave non-collectible until that design lands.
    {
        // The resolver reads the plugin's .deps.json to locate private dependencies.
        // Tolerate its absence: a plugin with no private deps may not ship one
        // (e.g. when loaded straight from a class-library build output).
        var depsFile = Path.ChangeExtension(entryAssemblyPath, ".deps.json");
        if (File.Exists(depsFile))
        {
            _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
        {
            return null;
        }

        // Shared with the host so contract/SDK/abstractions types unify across the
        // boundary; anything else probes the plugin's own dependencies first.
        if (SharedAssemblies.Contains(assemblyName.Name) ||
            assemblyName.Name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
        {
            return null;
        }

        var path = _resolver?.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver?.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
