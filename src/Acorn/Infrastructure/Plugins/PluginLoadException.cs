namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Thrown when a plugin that is explicitly enabled in configuration cannot be
///     discovered or loaded. Loading is fail-fast by design: an operator asked for
///     the plugin, so silently skipping it would hide a broken deployment.
/// </summary>
internal sealed class PluginLoadException : Exception
{
    public PluginLoadException(string message)
        : base(message)
    {
    }

    public PluginLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
