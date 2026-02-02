using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Options;

/// <summary>
///     Configuration for arena spawn points
/// </summary>
public class ArenaSpawn
{
    /// <summary>
    ///     Queue position where players stand to join (x,y)
    /// </summary>
    public required Coords From { get; set; }

    /// <summary>
    ///     Battle position where players are warped (x,y)
    /// </summary>
    public required Coords To { get; set; }
}

/// <summary>
///     Configuration for arena bot settings
/// </summary>
public class ArenaBotSettings
{
    /// <summary>
    ///     Whether bots are enabled for this arena
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Minimum number of bots to maintain in queue
    /// </summary>
    public int MinBots { get; set; } = 2;
}

/// <summary>
///     Configuration for a single arena
/// </summary>
public class Arena
{
    /// <summary>
    ///     Map ID where the arena is located
    /// </summary>
    public required int Map { get; set; }

    /// <summary>
    ///     Number of ticks between arena launches
    /// </summary>
    public required int Rate { get; set; }

    /// <summary>
    ///     Maximum number of players per match
    /// </summary>
    public required int Block { get; set; }

    /// <summary>
    ///     Spawn point configurations for queue and battle positions
    /// </summary>
    public required List<ArenaSpawn> Spawns { get; set; }

    /// <summary>
    ///     X coordinate where defeated players respawn
    /// </summary>
    public required int RespawnX { get; set; }

    /// <summary>
    ///     Y coordinate where defeated players respawn
    /// </summary>
    public required int RespawnY { get; set; }

    /// <summary>
    ///     Bot configuration for this arena (null if bots disabled)
    /// </summary>
    public ArenaBotSettings? BotSettings { get; set; }
}

/// <summary>
///     Configuration options for arena system
/// </summary>
public class ArenaOptions
{
    /// <summary>
    ///     List of configured arenas
    /// </summary>
    public List<Arena> Arenas { get; set; } = new();
}
