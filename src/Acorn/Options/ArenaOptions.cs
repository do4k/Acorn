namespace Acorn.Options;

public class ArenaOptions
{
    /// <summary>
    ///     Whether arena functionality is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Map ID where the arena takes place.
    /// </summary>
    public int ArenaMapId { get; set; } = 1;

    /// <summary>
    ///     Time in seconds between arena spawns.
    /// </summary>
    public int SpawnInterval { get; set; } = 30;

    /// <summary>
    ///     Minimum number of players in queue before arena starts spawning.
    /// </summary>
    public int MinPlayersToBlock { get; set; } = 2;

    /// <summary>
    ///     Number of kills needed to win the arena (0 = no win condition, free-for-all).
    /// </summary>
    public int KillsToWin { get; set; } = 0;

    public static string SectionName => "Arena";
}

public class ArenaSpawn
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
}
