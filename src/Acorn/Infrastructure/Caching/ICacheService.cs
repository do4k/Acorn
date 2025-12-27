namespace Acorn.Infrastructure.Caching;

/// <summary>
/// Interface for caching service to reduce database load.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a value from cache by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key);
    
    /// <summary>
    /// Set a value in cache with optional expiry.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    
    /// <summary>
    /// Remove a value from cache.
    /// </summary>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Check if a key exists in cache.
    /// </summary>
    Task<bool> ExistsAsync(string key);
    
    /// <summary>
    /// Remove all keys matching a pattern (e.g., "character:*").
    /// </summary>
    Task RemoveByPatternAsync(string pattern);
}
