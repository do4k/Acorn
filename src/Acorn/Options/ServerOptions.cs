namespace Acorn.Options;

public class ServerOptions
{
    public required NewCharacterOptions NewCharacter { get; set; }
    public required HostingOptions Hosting { get; set; }
    public required int TickRate { get; set; }
    /// <summary>
    /// How often players recover HP/TP, in ticks.
    /// With TickRate=1000 (1 second), 90 = every 90 seconds.
    /// reoserv uses 720 ticks at 125ms = 90 seconds.
    /// </summary>
    public int PlayerRecoverRate { get; set; } = 90;
    /// <summary>
    /// Player respawn location when they die. Falls back to NewCharacter location if not set.
    /// </summary>
    public RescueOptions? Rescue { get; set; }
    /// <summary>
    /// Whether to enforce packet sequence validation. Disable for debugging.
    /// </summary>
    public bool EnforceSequence { get; set; } = true;
    /// <summary>
    /// Whether to log packet contents at debug level. Can be very verbose.
    /// </summary>
    public bool LogPackets { get; set; } = false;
}