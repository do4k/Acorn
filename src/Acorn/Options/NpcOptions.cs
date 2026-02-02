namespace Acorn.Options;

/// <summary>
///     Configuration options for NPC behavior
/// </summary>
public class NpcOptions
{
    /// <summary>
    ///     Speed configuration for each spawn type (0-6)
    ///     Values represent tick intervals between actions
    /// </summary>
    public int Speed0 { get; set; } = 4;
    public int Speed1 { get; set; } = 8;
    public int Speed2 { get; set; } = 12;
    public int Speed3 { get; set; } = 16;
    public int Speed4 { get; set; } = 24;
    public int Speed5 { get; set; } = 32;
    public int Speed6 { get; set; } = 48;

    /// <summary>
    ///     Number of ticks an opponent can go without attacking before losing aggro
    /// </summary>
    public int BoredThreshold { get; set; } = 60;

    /// <summary>
    ///     Maximum distance (in tiles) an NPC will chase a target
    /// </summary>
    public int ChaseDistance { get; set; } = 10;

    /// <summary>
    ///     Whether NPCs spawn instantly when the map loads (true) or wait their spawn time (false)
    /// </summary>
    public bool InstantSpawn { get; set; } = true;

    /// <summary>
    ///     Variance in tiles (±) from spawn point for non-fixed NPCs
    /// </summary>
    public int SpawnVariance { get; set; } = 2;

    /// <summary>
    ///     Maximum attempts to find a valid spawn position
    /// </summary>
    public int MaxSpawnAttempts { get; set; } = 200;
}
