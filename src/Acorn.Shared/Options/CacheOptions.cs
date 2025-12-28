namespace Acorn.Shared.Options;

public class CacheOptions
{
    /// <summary>
    /// Whether caching is enabled. If false, uses no-op in-memory cache.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Redis connection string (e.g., "localhost:6379" or "redis:6379,password=xxx").
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";
    
    /// <summary>
    /// Default cache expiration time in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 5;
    
    /// <summary>
    /// Whether to use Redis (true) or in-memory cache (false).
    /// </summary>
    public bool UseRedis { get; set; } = true;
    
    /// <summary>
    /// Whether to log cache operations (get, set, remove) for debugging.
    /// </summary>
    public bool LogOperations { get; set; } = false;
}

