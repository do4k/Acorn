using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Acorn.Shared.Caching;

/// <summary>
/// Redis-based caching service for high-performance data access.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RedisCacheService>? _logger;
    private readonly bool _logOperations;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService>? logger = null, bool logOperations = false)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        _logOperations = logOperations;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
        {
            if (_logOperations)
                _logger?.LogDebug("[Redis] GET {Key} -> MISS", key);
            return default;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
            if (_logOperations)
                _logger?.LogDebug("[Redis] GET {Key} -> HIT ({Type})", key, typeof(T).Name);
            return result;
        }
        catch
        {
            // If deserialization fails, remove invalid cache entry
            await _db.KeyDeleteAsync(key);
            if (_logOperations)
                _logger?.LogDebug("[Redis] GET {Key} -> INVALID (removed)", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);

        if (expiry.HasValue)
        {
            await _db.StringSetAsync(key, json, expiry.Value);
            if (_logOperations)
                _logger?.LogDebug("[Redis] SET {Key} (expires in {Expiry})", key, expiry.Value);
        }
        else
        {
            await _db.StringSetAsync(key, json);
            if (_logOperations)
                _logger?.LogDebug("[Redis] SET {Key} (no expiry)", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
        if (_logOperations)
            _logger?.LogDebug("[Redis] DEL {Key}", key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        var exists = await _db.KeyExistsAsync(key);
        if (_logOperations)
            _logger?.LogDebug("[Redis] EXISTS {Key} -> {Result}", key, exists);
        return exists;
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();

        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }

        if (_logOperations)
            _logger?.LogDebug("[Redis] DEL pattern {Pattern} -> {Count} keys removed", pattern, keys.Length);
    }
}

