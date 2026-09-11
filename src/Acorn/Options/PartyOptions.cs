using Acorn.World.Services.Party;

namespace Acorn.Options;

public class PartyOptions
{
    /// <summary>Experience sharing mode used when a party kills an NPC.</summary>
    public PartyShareMode ShareMode { get; set; } = PartyShareMode.Equal;

    /// <summary>Maximum number of players allowed in a party.</summary>
    public int MaxPartySize { get; set; } = 9;

    public static string SectionName => "Party";
}
