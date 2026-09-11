using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Outcome of spending a stat or skill point.
/// </summary>
public enum StatSkillSpendResult
{
    Success,
    NoStatPoints,
    NoSkillPoints,
    MaxStatReached,
    MaxSkillLevelReached,
    UnknownSpell,
    InvalidStat
}

/// <summary>
///     Result of spending a point, including the skill's new level on success.
/// </summary>
public record StatSkillSpendOutcome(StatSkillSpendResult Result, int SkillLevel = 0)
{
    public bool Success => Result == StatSkillSpendResult.Success;
}

/// <summary>
///     Applies stat/skill point spending and character resets, enforcing the
///     configured MaxStat / MaxSkillLevel caps.
/// </summary>
public interface IStatSkillService
{
    /// <summary>
    ///     Spend one stat point on <paramref name="statId"/> and recalculate derived stats.
    /// </summary>
    StatSkillSpendOutcome AddStatPoint(Character character, StatId statId, Ecf classes);

    /// <summary>
    ///     Spend one skill point to level up <paramref name="spellId"/>.
    /// </summary>
    StatSkillSpendOutcome AddSkillPoint(Character character, int spellId);

    /// <summary>
    ///     Reset all base stats and learned skills, returning the stat/skill points
    ///     the character has earned for their level.
    /// </summary>
    void Reset(Character character, Ecf classes);
}
