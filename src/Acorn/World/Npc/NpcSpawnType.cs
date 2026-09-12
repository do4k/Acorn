namespace Acorn.World.Npc;

/// <summary>
///     NPC spawn type determines movement speed
///     Based on eoserv speed_table: 0.9, 0.6, 1.3, 1.9, 3.7, 7.5, 15.0, stationary
/// </summary>
public enum NpcSpawnType
{
    /// <summary>
    ///     Spawn type 0 - Speed 0.9 seconds between actions
    /// </summary>
    Type0 = 0,

    /// <summary>
    ///     Spawn type 1 - Speed 0.6 seconds (faster)
    /// </summary>
    Type1 = 1,

    /// <summary>
    ///     Spawn type 2 - Speed 1.3 seconds
    /// </summary>
    Type2 = 2,

    /// <summary>
    ///     Spawn type 3 - Speed 1.9 seconds
    /// </summary>
    Type3 = 3,

    /// <summary>
    ///     Spawn type 4 - Speed 3.7 seconds (slower)
    /// </summary>
    Type4 = 4,

    /// <summary>
    ///     Spawn type 5 - Speed 7.5 seconds (very slow)
    /// </summary>
    Type5 = 5,

    /// <summary>
    ///     Spawn type 6 - Speed 15.0 seconds (extremely slow)
    /// </summary>
    Type6 = 6,

    /// <summary>
    ///     Spawn type 7 - Stationary, never moves (direction set by spawn_time param)
    /// </summary>
    Stationary = 7
}
