using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;

namespace Acorn.World;

/// <summary>
/// Implementation of IWorldQueries that wraps WorldState singleton.
/// Provides a cleaner interface for querying world state without exposing internal collections.
/// </summary>
public class WorldStateQueries : IWorldQueries
{
    private readonly WorldState _world;
    private readonly IDataFileRepository _dataRepository;
    private readonly ILogger<WorldStateQueries> _logger;

    public WorldStateQueries(WorldState world, IDataFileRepository dataRepository, ILogger<WorldStateQueries> logger)
    {
        _world = world;
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public IDataFileRepository DataRepository => _dataRepository;

    public MapState? FindMap(int mapId)
        => _world.MapForId(mapId);

    public IEnumerable<PlayerState> GetPlayersInMap(int mapId)
        => _world.Players.Values.Where(p => p.Character?.Map == mapId);

    public IEnumerable<PlayerState> GetAllPlayers()
        => _world.Players.Values;

    public IEnumerable<PlayerState> GetGlobalChatListeners()
        => _world.Players.Values.Where(p => p.IsListeningToGlobal);

    public bool IsPlayerOnline(string username)
        => _world.LoggedIn(username);

    public PlayerState? GetPlayer(int sessionId)
        => _world.Players.TryGetValue(sessionId, out var player) ? player : null;

    public IEnumerable<MapState> GetAllMaps()
        => _world.Maps.Values;

    public IEnumerable<GlobalMessage> GetRecentGlobalMessages(int count = 10)
        => _world.GlobalMessages
            .Values
            .OrderByDescending(x => x.CreatedAt)
            .Take(count);

    public void AddGlobalMessage(GlobalMessage message)
    {
        var success = _world.GlobalMessages.TryAdd(message.Id, message);
        if (!success)
        {
            _logger.LogWarning("Could not add global message {messageId} to WorldState", message.Id);
        }

        if (_world.GlobalMessages.Count <= 100)
        {
            return;
        }

        var oldestMessages = _world.GlobalMessages
            .Values
            .OrderBy(x => x.CreatedAt);

        foreach (var oldMessage in oldestMessages.Take(_world.GlobalMessages.Count - 100))
        {
            var removeSuccess = _world.GlobalMessages.TryRemove(oldMessage.Id, out _);
            if (!removeSuccess)
            {
                _logger.LogWarning("Could not remove old global message {messageId} from WorldState", oldMessage.Id);
            }
        }
    }
}
