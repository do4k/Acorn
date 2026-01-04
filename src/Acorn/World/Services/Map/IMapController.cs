using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

public interface IMapController
{
    /// <summary>
    ///     Attempt to sit a player in a chair at the specified coordinates.
    /// </summary>
    Task<bool> SitInChairAsync(PlayerState player, Coords coords, MapState map);

    /// <summary>
    ///     Stand a player up from a chair.
    /// </summary>
    Task<bool> StandFromChairAsync(PlayerState player, MapState map);

    /// <summary>
    ///     Process all NPC respawns for the map.
    /// </summary>
    Task ProcessNpcRespawnsAsync(MapState map);

    /// <summary>
    ///     Process NPC movement and combat for all alive NPCs.
    ///     Returns the set of player IDs who were attacked this tick.
    /// </summary>
    Task<HashSet<int>> ProcessNpcActionsAsync(MapState map);

    /// <summary>
    ///     Process player recovery (HP/TP regeneration).
    /// </summary>
    Task ProcessPlayerRecoveryAsync(MapState map, HashSet<int> excludePlayerIds);

    /// <summary>
    ///     Decrease item protection timers.
    /// </summary>
    void ProcessItemProtection(MapState map);
}