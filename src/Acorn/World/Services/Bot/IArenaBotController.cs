using Acorn.Options;
using Acorn.World.Bot;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Bot;

/// <summary>
///     Controller for arena bot AI logic and actions.
///     Handles bot movement, combat decisions, and targeting.
/// </summary>
public interface IArenaBotController
{
    /// <summary>
    ///     Finds the nearest enemy (player or bot) to attack.
    /// </summary>
    /// <returns>Tuple of (isBot, targetId, targetX, targetY) or null if no enemies found</returns>
    (bool isBot, int targetId, int targetX, int targetY)? FindNearestEnemy(ArenaBotState bot, MapState map);

    /// <summary>
    ///     Moves bot toward target coordinates.
    /// </summary>
    Task MoveTowardAsync(ArenaBotState bot, int targetX, int targetY, MapState map);

    /// <summary>
    ///     Makes bot wander randomly within arena bounds.
    /// </summary>
    Task WanderAsync(ArenaBotState bot, MapState map, Acorn.Options.Arena arenaConfig);

    /// <summary>
    ///     Bot attacks a target (player or bot).
    /// </summary>
    Task AttackAsync(ArenaBotState bot, int targetId, bool isBot, MapState map);

    /// <summary>
    ///     Bot performs idle animation (face direction changes, emotes).
    /// </summary>
    Task IdleAnimationAsync(ArenaBotState bot, MapState map);
}
