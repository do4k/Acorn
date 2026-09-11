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
    ///     Jail location used by admin commands. Falls back to Rescue, then NewCharacter,
    ///     when not configured.
    /// </summary>
    public JailOptions? Jail { get; set; }

    /// <summary>
    ///     Admin command flood rates reported to the client in Welcome_Reply server settings.
    /// </summary>
    public FloodRateOptions FloodRates { get; set; } = new();

    /// <summary>
    ///     Whether to enforce packet sequence validation. Disable for debugging.
    /// </summary>
    public bool EnforceSequence { get; set; } = true;

    /// <summary>
    ///     Whether to validate the client-supplied timestamp on walk (and other
    ///     action) packets. When enabled, walk packets whose timestamp is less than
    ///     36 units ahead of the player's last timestamp are ignored. Matches
    ///     eoserv's EnforceTimestamps option.
    /// </summary>
    public bool EnforceTimestamps { get; set; } = true;

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
    ///     Maximum number of characters allowed in a chat message. Longer messages
    ///     are truncated with a " [...]" suffix. Mirrors eoserv's ChatLength.
    /// </summary>
    public int ChatLength { get; set; } = 128;

    /// <summary>
    ///     Maximum rendered width (in pixels) of a chat message. Mirrors eoserv's
    ///     ChatMaxWidth. Uses the Endless Online bitmap font metrics.
    /// </summary>
    public int ChatMaxWidth { get; set; } = 1400;

    /// <summary>
    ///     Duration of a mute applied by the $mute command, in seconds.
    ///     Mirrors eoserv's MuteLength.
    /// </summary>
    public int MuteLengthSeconds { get; set; } = 90;

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

    /// <summary>
    ///     Maximum number of characters an account may have. Mirrors eoserv's MaxCharacters.
    /// </summary>
    public int MaxCharacters { get; set; } = 3;

    /// <summary>
    ///     When true, the first character created while no admin characters exist is
    ///     granted High Game Master status. Mirrors eoserv's FirstCharacterAdmin.
    /// </summary>
    public bool FirstCharacterAdmin { get; set; } = true;

    /// <summary>
    ///     Minimum account username length. Mirrors eoserv's AccountMinLength.
    /// </summary>
    public int AccountMinLength { get; set; } = 4;

    /// <summary>
    ///     Maximum account username length. Mirrors eoserv's AccountMaxLength.
    /// </summary>
    public int AccountMaxLength { get; set; } = 16;

    /// <summary>
    ///     Minimum account password length. Mirrors eoserv's PasswordMinLength.
    /// </summary>
    public int PasswordMinLength { get; set; } = 6;

    /// <summary>
    ///     Maximum account password length. Mirrors eoserv's PasswordMaxLength.
    /// </summary>
    public int PasswordMaxLength { get; set; } = 12;

    /// <summary>
    ///     Minimum hair style accepted during character creation. Mirrors eoserv's CreateMinHairStyle.
    /// </summary>
    public int CreateMinHairStyle { get; set; } = 1;

    /// <summary>
    ///     Maximum hair style accepted during character creation. Mirrors eoserv's CreateMaxHairStyle.
    /// </summary>
    public int CreateMaxHairStyle { get; set; } = 20;

    /// <summary>
    ///     Minimum hair color accepted during character creation. Mirrors eoserv's CreateMinHairColor.
    /// </summary>
    public int CreateMinHairColor { get; set; } = 0;

    /// <summary>
    ///     Maximum hair color accepted during character creation. Mirrors eoserv's CreateMaxHairColor.
    /// </summary>
    public int CreateMaxHairColor { get; set; } = 9;

    /// <summary>
    ///     Minimum skin accepted during character creation. Mirrors eoserv's CreateMinSkin.
    /// </summary>
    public int CreateMinSkin { get; set; } = 0;

    /// <summary>
    ///     Maximum skin accepted during character creation. Mirrors eoserv's CreateMaxSkin.
    /// </summary>
    public int CreateMaxSkin { get; set; } = 3;

    public static string SectionName => "Server";
}
