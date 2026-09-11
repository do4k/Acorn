namespace Acorn.Options;

/// <summary>
///     Configuration for guild behaviour. Defaults mirror eoserv's configuration values so that
///     out-of-the-box behaviour matches the reference server.
/// </summary>
public class GuildOptions
{
    public static string SectionName => "Guild";

    /// <summary>Gold cost to found a guild.</summary>
    public int Price { get; set; } = 50_000;

    /// <summary>Maximum number of members a guild may have.</summary>
    public int MaxMembers { get; set; } = 5_000;

    /// <summary>
    ///     Number of members (including the leader) required before a guild can be founded.
    ///     A value of 1 allows solo creation; values greater than 1 require recruiting nearby players.
    /// </summary>
    public int CreateMembers { get; set; } = 1;

    /// <summary>Maximum gold the guild bank can hold.</summary>
    public int BankMax { get; set; } = 2_000_000_000;

    /// <summary>Minimum gold deposit accepted by the guild bank.</summary>
    public int MinDeposit { get; set; } = 1_000;

    /// <summary>Cost deducted from the guild bank when recruiting a new member.</summary>
    public int RecruitCost { get; set; } = 1_000;

    /// <summary>Comma separated list of the nine default rank names.</summary>
    public string DefaultRanks { get; set; } = "Leader,Recruiter,,,,,,,New Member";

    /// <summary>Whether recruiters are listed in the guild info staff list.</summary>
    public bool ShowRecruiters { get; set; } = true;

    /// <summary>Whether custom per-member rank names are used.</summary>
    public bool CustomRanks { get; set; } = false;

    /// <summary>Highest rank allowed to edit the guild description and rank list.</summary>
    public int EditRank { get; set; } = 1;

    /// <summary>Highest rank allowed to kick members.</summary>
    public int KickRank { get; set; } = 1;

    /// <summary>Highest rank allowed to promote members.</summary>
    public int PromoteRank { get; set; } = 1;

    /// <summary>Highest rank allowed to promote members to the same rank.</summary>
    public int PromoteSameRank { get; set; } = 1;

    /// <summary>Highest rank allowed to demote members.</summary>
    public int DemoteRank { get; set; } = 1;

    /// <summary>Highest rank allowed to recruit members.</summary>
    public int RecruitRank { get; set; } = 2;

    /// <summary>Highest rank allowed to disband the guild.</summary>
    public int DisbandRank { get; set; } = 0;

    /// <summary>Whether multiple founders (rank 0) may exist.</summary>
    public bool MultipleFounders { get; set; } = true;

    /// <summary>Whether guild join/leave/kick events are announced.</summary>
    public bool Announce { get; set; } = true;

    /// <summary>Date format used for the guild creation date.</summary>
    public string DateFormat { get; set; } = "yyyy/MM/dd";

    /// <summary>Maximum guild tag length.</summary>
    public int MaxTagLength { get; set; } = 3;

    /// <summary>Minimum guild tag length.</summary>
    public int MinTagLength { get; set; } = 2;

    /// <summary>Maximum guild name length.</summary>
    public int MaxNameLength { get; set; } = 24;

    /// <summary>Maximum guild description length.</summary>
    public int MaxDescLength { get; set; } = 240;

    /// <summary>Maximum guild rank name length.</summary>
    public int MaxRankLength { get; set; } = 16;

    /// <summary>Maximum guild description line width.</summary>
    public int MaxWidth { get; set; } = 180;
}
