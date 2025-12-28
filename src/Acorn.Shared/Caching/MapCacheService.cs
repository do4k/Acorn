using Acorn.Shared.Models.Maps;

namespace Acorn.Shared.Caching;

/// <summary>
/// Redis-based map state cache service.
/// </summary>
public class MapCacheService : IMapCacheService
{
    private readonly ICacheService _cache;
    
    private const string MapStateKey = "map:state:";
    private const string MapListKey = "maps:all";

    public MapCacheService(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task CacheMapStateAsync(MapStateRecord mapState)
    {
        await _cache.SetAsync($"{MapStateKey}{mapState.Id}", mapState, TimeSpan.FromSeconds(30));
        
        // Update map list
        var mapList = await _cache.GetAsync<List<int>>(MapListKey) ?? [];
        if (!mapList.Contains(mapState.Id))
        {
            mapList.Add(mapState.Id);
            await _cache.SetAsync(MapListKey, mapList);
        }
    }

    public async Task<MapStateRecord?> GetMapStateAsync(int mapId)
    {
        return await _cache.GetAsync<MapStateRecord>($"{MapStateKey}{mapId}");
    }

    public async Task<IReadOnlyList<MapStateRecord>> GetAllMapStatesAsync()
    {
        var mapList = await _cache.GetAsync<List<int>>(MapListKey) ?? [];
        var results = new List<MapStateRecord>();
        
        foreach (var mapId in mapList)
        {
            var state = await GetMapStateAsync(mapId);
            if (state != null)
            {
                results.Add(state);
            }
        }
        
        return results;
    }

    public async Task<IReadOnlyList<MapSummary>> GetMapSummariesAsync()
    {
        var states = await GetAllMapStatesAsync();
        return states.Select(s => new MapSummary
        {
            Id = s.Id,
            Name = s.Name,
            PlayerCount = s.PlayerCount,
            NpcCount = s.NpcCount
        }).ToList();
    }

    public async Task RemoveMapStateAsync(int mapId)
    {
        await _cache.RemoveAsync($"{MapStateKey}{mapId}");
    }
}

