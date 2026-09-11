namespace Acorn.Options;

public class ServerOptions
{
    public required NewCharacterOptions NewCharacter { get; set; }
    public required HostingOptions Hosting { get; set; }
    public required int TickRate { get; set; }

    /// <summary>
    ///     How often players recover HP/TP, in ticks.
    ///     With TickRate=1000 (1 second), 90 = every 90 seconds.
    /// </summary>
    public int PlayerRecoverRate { get; set; } = 90;

    /// <summary>
    ///     How long items remain protected for the killer, in ticks.
    ///     With TickRate=1000 (1 second), 60 = 60 seconds of protection.
    /// </summary>
    public int DropProtectionTicks { get; set; } = 60;

    /// <summary>
    ///     Maximum amount of a single item stack that can be dropped at once.
    ///     Matches eoserv's MaxDrop setting. A value of 0 or less disables the clamp.
    /// </summary>
    public int MaxDrop { get; set; } = 10000;

    /// <summary>
    ///     Player respawn location when they die. Falls back to NewCharacter location if not set.
    /// </summary>
    public RescueOptions? Rescue { get; set; }

    /// <summary>
    ///     Whether to enforce packet sequence validation. Disable for debugging.
    /// </summary>
    public bool EnforceSequence { get; set; } = true;

    /// <summary>
    ///     How often the server sends Connection_Player ping packets to connected
    ///     players, in seconds. Reoserv uses ~7.5s (60 ticks × 125ms).
    /// </summary>
    public int PlayerPingIntervalSeconds { get; set; } = 8;

    /// <summary>
    ///     How long the ping hosted service waits after startup before sending the
    ///     first Connection_Player ping, in seconds.
    /// </summary>
    public int PlayerPingInitialDelaySeconds { get; set; } = 5;

    /// <summary>
    ///     Whether to log packet contents at debug level. Can be very verbose.
    /// </summary>
    public bool LogPackets { get; set; } = false;

    /// <summary>
    ///     Maximum distance (in tiles) a ranged attack can reach. Matches eoserv's
    ///     RangedDistance default. Melee attacks always use a range of 1.
    /// </summary>
    public int RangedDistance { get; set; } = 5;

    /// <summary>
    ///     Minimum time between attacks, in milliseconds. Replaces the previously
    ///     hard-coded 500ms cooldown.
    /// </summary>
    public int AttackCooldownMs { get; set; } = 500;

    /// <summary>
    ///     Whether the first hit against a full-health target is always a critical hit.
    ///     Matches eoserv's CriticalFirstHit (default false). When false, critical hits
    ///     only occur when attacking a target from behind or the side.
    /// </summary>
    public bool CriticalFirstHit { get; set; } = false;

    /// <summary>
    ///     Maximum value a single base stat (Str/Int/Wis/Agi/Con/Cha) can reach.
    ///     Matches eoserv's MaxStat default.
    /// </summary>
    public int MaxStat { get; set; } = 10000;

    /// <summary>
    ///     Maximum level a single learned skill/spell can reach.
    ///     Matches eoserv's MaxSkillLevel default.
    /// </summary>
    public int MaxSkillLevel { get; set; } = 100;

    /// <summary>
    ///     Number of stat points granted per character level.
    /// </summary>
    public int StatPerLevel { get; set; } = 3;

    /// <summary>
    ///     Number of skill points granted per character level.
    ///     Matches eoserv's SkillPerLevel default.
    /// </summary>
    public int SkillPerLevel { get; set; } = 4;

    public static string SectionName => "Server";
}