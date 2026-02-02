using Acorn.World.Bot;
using Acorn.World.Map;

namespace Acorn.World.Services.Bot;

/// <summary>
///     Service for managing arena bots.
///     Handles bot spawning, queue management, and lifecycle.
/// </summary>
public interface IArenaBotService
{
    /// <summary>
    ///     Spawns bots to fill empty queue spots when needed.
    ///     Called during arena tick processing.
    /// </summary>
    /// <param name="map">The map state containing the arena</param>
    Task SpawnBotsForQueueAsync(MapState map);

    /// <summary>
    ///     Removes bots from queue when real players join.
    /// </summary>
    /// <param name="map">The map state containing the arena</param>
    /// <param name="slotsNeeded">Number of slots to free up</param>
    Task RemoveBotsFromQueueAsync(MapState map, int slotsNeeded);

    /// <summary>
    ///     Processes bot AI actions (movement and combat).
    ///     Called every server tick for maps with active arena bots.
    /// </summary>
    /// <param name="map">The map state containing the arena</param>
    Task ProcessBotActionsAsync(MapState map);

    /// <summary>
    ///     Handles bot death in arena combat.
    /// </summary>
    /// <param name="bot">The bot that was killed</param>
    /// <param name="map">The map state containing the arena</param>
    Task HandleBotDeathAsync(ArenaBotState bot, MapState map);

    /// <summary>
    ///     Cleans up all bots when arena ends.
    /// </summary>
    /// <param name="map">The map state containing the arena</param>
    Task ClearBotsAsync(MapState map);
}
