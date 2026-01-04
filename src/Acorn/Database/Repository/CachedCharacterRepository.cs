using Acorn.Database.Models;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

/// <summary>
///     Cached wrapper for CharacterRepository to improve read performance.
///     Implements write-through caching strategy.
/// </summary>
public class CachedCharacterRepository : IDbRepository<Character>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly ICacheService _cache;
    private readonly IDbRepository<Character> _inner;
    private readonly ILogger<CachedCharacterRepository> _logger;

    public CachedCharacterRepository(
        IDbRepository<Character> inner,
        ICacheService cache,
        ILogger<CachedCharacterRepository> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Character?> GetByKeyAsync(string name)
    {
        var cacheKey = $"character:name:{name.ToLower()}";

        // Try cache first
        var cached = await _cache.GetAsync<Character>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for character name {CharacterName}", name);
            return cached;
        }

        // Cache miss - get from database
        _logger.LogDebug("Cache miss for character name {CharacterName}", name);
        var character = await _inner.GetByKeyAsync(name);

        if (character != null)
        {
            await _cache.SetAsync(cacheKey, character, CacheDuration);
        }

        return character;
    }

    public async Task<IEnumerable<Character>> GetAllAsync()
    {
        // Don't cache full list - too memory intensive
        return await _inner.GetAllAsync();
    }

    public async Task CreateAsync(Character entity)
    {
        await _inner.CreateAsync(entity);

        // Write through to cache
        if (entity.Name != null)
        {
            await _cache.SetAsync($"character:name:{entity.Name.ToLower()}", entity, CacheDuration);
        }

        _logger.LogDebug("Created and cached character {CharacterName}", entity.Name);
    }

    public async Task UpdateAsync(Character entity)
    {
        await _inner.UpdateAsync(entity);

        // Update cache
        if (entity.Name != null)
        {
            await _cache.SetAsync($"character:name:{entity.Name.ToLower()}", entity, CacheDuration);
        }

        _logger.LogDebug("Updated and cached character {CharacterName}", entity.Name);
    }

    public async Task DeleteAsync(Character entity)
    {
        await _inner.DeleteAsync(entity);

        // Invalidate cache
        if (entity.Name != null)
        {
            await _cache.RemoveAsync($"character:name:{entity.Name.ToLower()}");
        }

        _logger.LogDebug("Deleted and invalidated cache for character {CharacterName}", entity.Name);
    }
}