using Acorn.Shared.Caching;
using Acorn.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds in-memory caching services.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddSingleton<ICacheService>(sp =>
        {
            var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<InMemoryCacheService>>();

            if (!cacheOptions.Enabled)
            {
                logger.LogInformation("Caching is disabled");
            }
            else
            {
                logger.LogInformation("Using in-memory cache");
            }

            return new InMemoryCacheService();
        });

        // Register PubCacheService for pub file caching
        services.AddSingleton<IPubCacheService, PubCacheService>();

        // Register MapCacheService for realtime map state caching
        services.AddSingleton<IMapCacheService, MapCacheService>();

        // Register CharacterCacheService for online character caching
        services.AddSingleton<ICharacterCacheService, CharacterCacheService>();

        return services;
    }
}
