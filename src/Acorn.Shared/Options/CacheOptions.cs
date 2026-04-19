namespace Acorn.Shared.Options;

public class CacheOptions
{
    /// <summary>
    /// Whether caching is enabled. If false, cache operations are no-ops.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default cache expiration time in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to log cache operations (get, set, remove) for debugging.
    /// </summary>
    public bool LogOperations { get; set; } = false;

    public static string SectionName => "Cache";
}
