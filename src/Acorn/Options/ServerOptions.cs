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

    public static string SectionName => "Server";
}
