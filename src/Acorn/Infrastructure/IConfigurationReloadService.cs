namespace Acorn.Infrastructure;

/// <summary>
///     Re-reads the server's configuration sources (JSON files, environment variables).
/// </summary>
public interface IConfigurationReloadService
{
    /// <summary>
    ///     Reloads configuration from its sources.
    /// </summary>
    /// <returns><c>true</c> when the reload succeeded; otherwise <c>false</c>.</returns>
    bool Reload();
}
