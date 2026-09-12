using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Result of spending a point, including the skill's new level on success.
/// </summary>
public record StatSkillSpendOutcome(StatSkillSpendResult Result, int SkillLevel = 0)
{
    public bool Success => Result == StatSkillSpendResult.Success;
}
