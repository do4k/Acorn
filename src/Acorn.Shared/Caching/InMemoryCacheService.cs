using System.Text.RegularExpressions;

namespace Acorn.Shared.Caching;

/// <summary>
/// In-memory cache service for when Redis is not available.
/// Uses simple Dictionary with manual expiration tracking.
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, (object Value, DateTime? Expiry)> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<T?> GetAsync<T>(string key)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow)
                {
                    _cache.Remove(key);
                    return default;
                }
                return (T)entry.Value;
            }
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        await _lock.WaitAsync();
        try
        {
            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _cache[key] = (value!, expiryTime);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            _cache.Remove(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow)
                {
                    _cache.Remove(key);
                    return false;
                }
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        await _lock.WaitAsync();
        try
        {
            var regex = new Regex(
                "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
            
            var keysToRemove = _cache.Keys.Where(k => regex.IsMatch(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

