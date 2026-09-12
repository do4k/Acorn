using Acorn.Shared.Models.Maps;

namespace Acorn.Shared.Caching;

/// <summary>
/// Service for caching and retrieving map state data.
/// </summary>
public interface IMapCacheService
{
    /// <summary>
    /// Cache a map's current state.
    /// </summary>
    Task CacheMapStateAsync(MapStateRecord mapState);

    /// <summary>
    /// Get a map's current state by ID.
    /// </summary>
    Task<MapStateRecord?> GetMapStateAsync(int mapId);

    /// <summary>
    /// Get all cached map states.
    /// </summary>
    Task<IReadOnlyList<MapStateRecord>> GetAllMapStatesAsync();

    /// <summary>
    /// Get summary of all maps (without player/npc details).
    /// </summary>
    Task<IReadOnlyList<MapSummary>> GetMapSummariesAsync();

    /// <summary>
    /// Remove a map from cache.
    /// </summary>
    Task RemoveMapStateAsync(int mapId);
}
