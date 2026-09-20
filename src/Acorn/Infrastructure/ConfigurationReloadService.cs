using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

/// <summary>
///     Re-reads configuration from its registered sources.
/// </summary>
public class ConfigurationReloadService(
    IConfiguration configuration,
    ILogger<ConfigurationReloadService> logger) : IConfigurationReloadService
{
    public bool Reload()
    {
        if (configuration is not IConfigurationRoot root)
        {
            logger.LogWarning("Configuration does not support reloading");
            return false;
        }

        try
        {
            root.Reload();
            logger.LogInformation("Configuration reloaded from its sources");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload configuration");
            return false;
        }
    }
}
