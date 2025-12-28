using Acorn.Shared.Caching;
using Acorn.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Acorn.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds caching services (Redis or In-Memory) based on configuration.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddSingleton<ICacheService>(sp =>
        {
            var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();

            if (!cacheOptions.Enabled)
            {
                logger.LogInformation("Caching is disabled");
                return new InMemoryCacheService();
            }

            if (cacheOptions.UseRedis)
            {
                try
                {
                    var redis = ConnectionMultiplexer.Connect(cacheOptions.ConnectionString);
                    logger.LogInformation("Connected to Redis at {ConnectionString} (LogOperations: {LogOperations})",
                        cacheOptions.ConnectionString, cacheOptions.LogOperations);
                    return new RedisCacheService(redis, logger, cacheOptions.LogOperations);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to connect to Redis, falling back to in-memory cache");
                    return new InMemoryCacheService();
                }
            }

            logger.LogInformation("Using in-memory cache");
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

