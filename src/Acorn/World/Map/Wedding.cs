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

/// <summary>
///     Represents the stages of a wedding ceremony, matching the EO protocol flow.
/// </summary>
public enum WeddingState
{
    /// <summary>Wedding has been requested, waiting for partner to accept.</summary>
    Requested,

    /// <summary>Partner has accepted, ceremony is about to begin.</summary>
    Accepted,

    /// <summary>Priest dialog 1: introduction speech.</summary>
    PriestDialog1,

    /// <summary>Priest dialog 2: continued speech.</summary>
    PriestDialog2,

    /// <summary>Priest asks the partner "Do you..."</summary>
    PriestDoYouPartner,

    /// <summary>Sending the DoYou prompt to the partner client.</summary>
    AskPartner,

    /// <summary>Waiting for partner to say "I do".</summary>
    WaitingForPartner,

    /// <summary>Partner has agreed ("I do").</summary>
    PartnerAgrees,

    /// <summary>Priest asks the player "Do you..."</summary>
    PriestDoYouPlayer,

    /// <summary>Sending the DoYou prompt to the player client.</summary>
    AskPlayer,

    /// <summary>Waiting for player to say "I do".</summary>
    WaitingForPlayer,

    /// <summary>Player has agreed ("I do").</summary>
    PlayerAgrees,

    /// <summary>Priest dialog 3: pronouncement, rings, set partner.</summary>
    PriestDialog3,

    /// <summary>Priest dialog 4: congratulations.</summary>
    PriestDialog4,

    /// <summary>Hearts effect on both players.</summary>
    Hearts,

    /// <summary>Priest dialog 5 and confetti effect.</summary>
    PriestDialog5AndConfetti,

    /// <summary>Wedding is complete.</summary>
    Done
}
