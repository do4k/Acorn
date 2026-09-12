using Acorn.Data;
using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
///     Result of validating a skill purchase, including the class id to show in
///     the client's "wrong class" message (the required class, or the player's
///     own class when there is no more specific requirement).
/// </summary>
public record SkillLearnValidation(SkillLearnFailure Failure, int WrongClassId = 0)
{
    public bool CanLearn => Failure == SkillLearnFailure.None;
}
