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

/// <summary>
///     Pure helpers for calculating how party experience is shared between members.
/// </summary>
public static class PartyExpCalculator
{
    /// <summary>
    ///     Calculate the experience awarded to a single party member.
    ///     Equal: ceil(exp / memberCount). LevelBased: ceil(exp * level / sumOfLevels).
    ///     A level of 0 is treated as level 1, matching eoserv.
    /// </summary>
    public static int CalculateShare(int totalExp, int memberLevel, int sumOfLevels, int memberCount,
        PartyShareMode mode)
    {
        if (totalExp <= 0 || memberCount <= 0 || sumOfLevels <= 0)
        {
            return 0;
        }

        if (mode == PartyShareMode.LevelBased)
        {
            var effectiveLevel = memberLevel == 0 ? 1 : memberLevel;
            return (int)Math.Ceiling(totalExp * (double)effectiveLevel / sumOfLevels);
        }

        return (int)Math.Ceiling(totalExp / (double)memberCount);
    }
}
