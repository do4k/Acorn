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
