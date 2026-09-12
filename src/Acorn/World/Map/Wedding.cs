namespace Acorn.World.Map;

/// <summary>
///     Tracks the state of an active wedding ceremony on a map.
/// </summary>
public class Wedding
{
    /// <summary>Session ID of the player who initiated the wedding.</summary>
    public required int PlayerSessionId { get; set; }

    /// <summary>Session ID of the partner being married.</summary>
    public required int PartnerSessionId { get; set; }

    /// <summary>NPC index of the priest NPC performing the ceremony.</summary>
    public required int NpcIndex { get; set; }

    /// <summary>Current state of the wedding ceremony.</summary>
    public WeddingState State { get; set; } = WeddingState.Requested;
}
