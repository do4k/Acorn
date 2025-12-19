using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.World.Map;
using Acorn.World.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World;

public class WorldState
{
    public ConcurrentDictionary<Guid, GlobalMessage> GlobalMessages = [];
    public ConcurrentDictionary<int, MapState> Maps = [];
    public ConcurrentDictionary<int, PlayerState> Players = [];
    private readonly ILogger<WorldState> _logger;

    public WorldState(
        IDataFileRepository dataRepository,
        MapStateFactory mapStateFactory,
        ILogger<WorldState> logger)
    {
        _logger = logger;
        foreach (var map in dataRepository.Maps)
        {
            var added = Maps.TryAdd(map.Id, mapStateFactory.Create(map));
            if (added is false)
            {
                _logger.LogWarning("Failed to add map {MapId} to world state", map.Id);
            }
        }
    }

    public MapState? MapForId(int mapId)
    {
        var exists = Maps.TryGetValue(mapId, out var map);
        if (exists is true && map is not null)
        {
            return map;
        }

        _logger.LogWarning("Map with id {MapId} does not exist in world state", mapId);
        return null;
    }

    public bool LoggedIn(string username)
        => Players.Values.Any(x => x.Account?.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase) ?? false);
}