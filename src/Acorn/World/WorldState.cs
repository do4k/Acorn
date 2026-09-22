using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World;

public class WorldState
{
    private readonly ILogger<WorldState> _logger;
    private readonly ConcurrentDictionary<Guid, GlobalMessage> _globalMessages = [];
    private readonly ConcurrentDictionary<int, MapState> _maps = [];
    private readonly ConcurrentDictionary<int, PlayerState> _players = [];
    private readonly MapStateFactory _mapStateFactory;

    public IReadOnlyDictionary<Guid, GlobalMessage> GlobalMessages => _globalMessages;

    public bool TryAddGlobalMessage(Guid id, GlobalMessage message) => _globalMessages.TryAdd(id, message);
    public bool TryRemoveGlobalMessage(Guid id) => _globalMessages.TryRemove(id, out _);
    public int GlobalMessageCount => _globalMessages.Count;
    public IReadOnlyDictionary<int, MapState> Maps => _maps;
    public IReadOnlyDictionary<int, PlayerState> Players => _players;

    public WorldState(
        IDataFileRepository dataRepository,
        MapStateFactory mapStateFactory,
        ILogger<WorldState> logger)
    {
        _logger = logger;
        _mapStateFactory = mapStateFactory;
        foreach (var map in dataRepository.Maps)
        {
            var added = _maps.TryAdd(map.Id, mapStateFactory.Create(map));
            if (added is false)
            {
                _logger.LogWarning("Failed to add map {MapId} to world state", map.Id);
            }
        }
    }

    /// <summary>
    ///     Replaces the live map state for <paramref name="map"/> with one built from
    ///     freshly loaded data. Fails if players are still on the map, because their
    ///     sessions hold a direct reference to the old state.
    /// </summary>
    public bool TryReplaceMap(MapWithId map)
    {
        if (_maps.TryGetValue(map.Id, out var existing) && existing.Players.Count > 0)
        {
            return false;
        }

        _maps[map.Id] = _mapStateFactory.Create(map);
        _logger.LogInformation("Replaced map {MapId} in world state with freshly loaded data", map.Id);
        return true;
    }

    public MapState? MapForId(int mapId)
    {
        var exists = _maps.TryGetValue(mapId, out var map);
        if (exists is true && map is not null)
        {
            return map;
        }

        _logger.LogWarning("Map with id {MapId} does not exist in world state", mapId);
        return null;
    }

    public bool LoggedIn(string username)
    {
        return _players.Values.Any(x =>
            x.Account?.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase) ?? false);
    }

    public bool TryAddPlayer(int sessionId, PlayerState player)
    {
        return _players.TryAdd(sessionId, player);
    }

    public bool TryRemovePlayer(int sessionId, out PlayerState? player)
    {
        return _players.TryRemove(sessionId, out player);
    }

    public PlayerState? GetPlayer(int sessionId)
    {
        return _players.TryGetValue(sessionId, out var player) ? player : null;
    }

    public IEnumerable<PlayerState> GetPlayersOnMap(int mapId)
    {
        return _players.Values.Where(p => p.Character?.Map == mapId);
    }

    public IEnumerable<PlayerState> GetGlobalListeners()
    {
        return _players.Values.Where(p => p.IsListeningToGlobal);
    }
}