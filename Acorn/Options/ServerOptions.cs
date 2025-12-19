namespace Acorn.Options;

public class ServerOptions
{
    public required NewCharacterOptions NewCharacter { get; set; }
    public required HostingOptions Hosting { get; set; }
    public required int TickRate { get; set; }
    /// <summary>
    /// Whether to enforce packet sequence validation. Disable for debugging.
    /// </summary>
    public bool EnforceSequence { get; set; } = true;
}