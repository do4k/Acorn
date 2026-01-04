using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.World.Map;

namespace Acorn.World;

/// <summary>
///     Provides queries and commands against the world state.
///     Use this instead of injecting WorldState directly for better testability and decoupling.
/// </summary>
public interface IWorldQueries
{
    /// <summary>
    ///     Data file repository for accessing EIF, ENF, etc
    /// </summary>
    IDataFileRepository DataRepository { get; }

    /// <summary>
    ///     Find a map by its ID
    /// </summary>
    MapState? FindMap(int mapId);

    /// <summary>
    ///     Get all players currently in a specific map
    /// </summary>
    IEnumerable<PlayerState> GetPlayersInMap(int mapId);

    /// <summary>
    ///     Get all online players
    /// </summary>
    IEnumerable<PlayerState> GetAllPlayers();

    /// <summary>
    ///     Get players who are listening to global chat
    /// </summary>
    IEnumerable<PlayerState> GetGlobalChatListeners();

    /// <summary>
    ///     Check if a player with the given username is currently online
    /// </summary>
    bool IsPlayerOnline(string username);

    /// <summary>
    ///     Get a player by their session ID
    /// </summary>
    PlayerState? GetPlayer(int sessionId);

    /// <summary>
    ///     Get all maps in the world
    /// </summary>
    IEnumerable<MapState> GetAllMaps();

    /// <summary>
    ///     Get recent global messages (for when a player opens global chat)
    /// </summary>
    IEnumerable<GlobalMessage> GetRecentGlobalMessages(int count = 10);

    /// <summary>
    ///     Add a new global message
    /// </summary>
    void AddGlobalMessage(GlobalMessage message);
}