using Acorn.Shared.Caching;

namespace Acorn.Api.Features;

public static class HealthFeature
{
    public static RouteGroupBuilder MapHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/health")
            .WithTags("Health");

        group.MapGet("/", GetHealth)
            .WithName("GetHealth")
            .WithDescription("Health check endpoint showing cache status")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetHealth(ICacheService cache)
    {
        var cacheType = cache.GetType().Name;
        var isRedis = cacheType == "RedisCacheService";

        // Test cache connectivity
        bool cacheHealthy;
        try
        {
            var testKey = "_health_check_";
            await cache.SetAsync(testKey, DateTime.UtcNow, TimeSpan.FromSeconds(5));
            var result = await cache.GetAsync<DateTime>(testKey);
            cacheHealthy = result != default;
            await cache.RemoveAsync(testKey);
        }
        catch
        {
            cacheHealthy = false;
        }

        var response = new HealthResponse
        {
            Status = cacheHealthy ? "Healthy" : "Degraded",
            Cache = new CacheStatus
            {
                Type = cacheType,
                IsRedis = isRedis,
                Healthy = cacheHealthy,
                Warning = !isRedis
                    ? "In-memory cache is active. API cannot access game server data without Redis."
                    : null
            },
            Timestamp = DateTime.UtcNow
        };

        return cacheHealthy
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

public record HealthResponse
{
    public required string Status { get; init; }
    public required CacheStatus Cache { get; init; }
    public DateTime Timestamp { get; init; }
}

public record CacheStatus
{
    public required string Type { get; init; }
    public bool IsRedis { get; init; }
    public bool Healthy { get; init; }
    public string? Warning { get; init; }
}

