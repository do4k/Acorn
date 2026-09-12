namespace Acorn.World.Services.Party;

/// <summary>
///     Experience sharing modes for a party. Values mirror eoserv's ShareEXP sharemode argument.
/// </summary>
public enum PartyShareMode
{
    /// <summary>Experience is split evenly between all eligible members.</summary>
    Equal = 1,

    /// <summary>Experience is split proportionally to each member's level.</summary>
    LevelBased = 2
}
