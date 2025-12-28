using Acorn.Shared.Caching;
using Acorn.Shared.Models.Maps;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure;

/// <summary>
/// Hosted service that periodically caches map state to Redis.
/// </summary>
public class MapCacheHostedService : BackgroundService
{
    private readonly WorldState _worldState;
    private readonly IMapCacheService _mapCache;
    private readonly ILogger<MapCacheHostedService> _logger;
    private readonly CacheOptions _cacheOptions;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);

    public MapCacheHostedService(
        WorldState worldState,
        IMapCacheService mapCache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<MapCacheHostedService> logger)
    {
        _worldState = worldState;
        _mapCache = mapCache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Map cache service started, updating every {Interval} seconds", _updateInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CacheAllMapsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching map states");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private async Task CacheAllMapsAsync()
    {
        var cachedCount = 0;

        foreach (var (mapId, mapState) in _worldState.Maps)
        {
            try
            {
                var players = mapState.Players
                    .Where(p => p.Character != null)
                    .Select(p => new MapPlayerRecord
                    {
                        SessionId = p.SessionId,
                        Name = p.Character!.Name ?? string.Empty,
                        X = p.Character.X,
                        Y = p.Character.Y,
                        Direction = p.Character.Direction.ToString(),
                        Level = p.Character.Level,
                        Hp = p.Character.Hp,
                        MaxHp = p.Character.MaxHp,
                        SitState = p.Character.SitState.ToString(),
                        Hidden = p.Character.Hidden
                    })
                    .ToList();

                var npcs = mapState.Npcs
                    .Select((npc, index) => new MapNpcRecord
                    {
                        Index = index,
                        Id = npc.Id,
                        Name = npc.Data.Name ?? string.Empty,
                        X = npc.X,
                        Y = npc.Y,
                        Direction = npc.Direction.ToString(),
                        Hp = npc.Hp,
                        MaxHp = npc.Data.Hp,
                        IsDead = npc.IsDead
                    })
                    .ToList();

                var items = mapState.Items
                    .Select(kvp => new MapItemRecord
                    {
                        UniqueId = kvp.Key,
                        ItemId = kvp.Value.Id,
                        Amount = kvp.Value.Amount,
                        X = kvp.Value.Coords.X,
                        Y = kvp.Value.Coords.Y
                    })
                    .ToList();

                var record = new MapStateRecord
                {
                    Id = mapId,
                    Name = mapState.Data.Name ?? $"Map {mapId}",
                    Width = mapState.Data.Width,
                    Height = mapState.Data.Height,
                    PlayerCount = players.Count,
                    NpcCount = npcs.Count(n => !n.IsDead),
                    ItemCount = items.Count,
                    Players = players,
                    Npcs = npcs,
                    Items = items
                };

                await _mapCache.CacheMapStateAsync(record);
                cachedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache map {MapId}", mapId);
            }
        }
        
        if (_cacheOptions.LogOperations)
            _logger.LogDebug("Cached {Count} maps to Redis", cachedCount);
    }
}

