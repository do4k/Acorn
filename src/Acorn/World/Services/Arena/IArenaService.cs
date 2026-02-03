using Acorn.Net;
using Acorn.World.Bot;
using Acorn.World.Map;

namespace Acorn.World.Services.Arena;

/// <summary>
///     Service responsible for managing arena matches, combat, and state.
/// </summary>
public interface IArenaService
{
    /// <summary>
    ///     Processes the timed arena system for a map. Checks queue, launches matches.
    /// </summary>
    Task ProcessTimedArenaAsync(MapState map);

    /// <summary>
    ///     Handles arena combat when a player attacks another in an arena.
    /// </summary>
    Task<bool> HandleArenaCombatAsync(PlayerState attacker, PlayerState target, MapState map);

    /// <summary>
    ///     Handles arena combat when a player attacks a bot in an arena.
    /// </summary>
    Task<bool> HandlePlayerAttackBotAsync(PlayerState attacker, ArenaBotState targetBot, MapState map);

    /// <summary>
    ///     Handles a player dying in the arena and respawning.
    /// </summary>
    Task HandleArenaDeathAsync(PlayerState player);

    /// <summary>
    ///     Handles a player abandoning the arena (leaving mid-match).
    /// </summary>
    Task HandleArenaAbandonmentAsync(PlayerState player, MapState map);

    /// <summary>
    ///     Checks if a player is currently in an active arena match on the map.
    /// </summary>
    bool IsPlayerInArena(PlayerState player, MapState map);
}
