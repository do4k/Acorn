using Acorn.Data;
using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
///     Why a character cannot learn a skill from a skill master.
/// </summary>
public enum SkillLearnFailure
{
    None,
    AlreadyKnown,
    InsufficientGold,
    LevelTooLow,
    StatsTooLow,
    MissingPrerequisite,
    WrongClass
}
