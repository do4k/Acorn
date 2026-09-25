using Acorn.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Host-provided <see cref="IPluginContext" /> handed to a plugin on load.
/// </summary>
internal sealed class PluginContext(
    PluginManifest manifest,
    IConfiguration config,
    ILogger log,
    IWorldApi world) : IPluginContext
{
    public string PluginId => manifest.Id;

    public string PluginVersion => manifest.Version;

    public IConfiguration Config { get; } = config;

    public ILogger Log { get; } = log;

    public IWorldApi World { get; } = world;
}
