using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

/// <summary>
///     Hosted service that caches pub file data on startup.
/// </summary>
public class PubFileCacheHostedService(
    IPubFileReloadService pubFileReload,
    ILogger<PubFileCacheHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Caching pub files...");

        try
        {
            await pubFileReload.RefreshCacheAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cache pub files");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
