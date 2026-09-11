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

/// <summary>
///     Result of validating a skill purchase, including the class id to show in
///     the client's "wrong class" message (the required class, or the player's
///     own class when there is no more specific requirement).
/// </summary>
public record SkillLearnValidation(SkillLearnFailure Failure, int WrongClassId = 0)
{
    public bool CanLearn => Failure == SkillLearnFailure.None;
}

/// <summary>
///     Evaluates whether a character meets the requirements to learn a skill
///     from a skill master. Mirrors eoserv's StatSkill_Take checks.
/// </summary>
public static class SkillLearnValidator
{
    public static SkillLearnValidation Validate(Character character, SkillMasterSkill skill, int goldAmount)
    {
        if (character.Spells.Items.Any(s => s.Id == skill.SkillId))
        {
            return new SkillLearnValidation(SkillLearnFailure.AlreadyKnown);
        }

        if (goldAmount < skill.Price)
        {
            return new SkillLearnValidation(SkillLearnFailure.InsufficientGold, character.Class);
        }

        if (skill.LevelRequirement > 0 && character.Level < skill.LevelRequirement)
        {
            return new SkillLearnValidation(SkillLearnFailure.LevelTooLow, character.Class);
        }

        if (character.AdjStr < skill.StrRequirement ||
            character.AdjInt < skill.IntRequirement ||
            character.AdjWis < skill.WisRequirement ||
            character.AdjAgi < skill.AgiRequirement ||
            character.AdjCon < skill.ConRequirement ||
            character.AdjCha < skill.ChaRequirement)
        {
            return new SkillLearnValidation(SkillLearnFailure.StatsTooLow, character.Class);
        }

        if (skill.SkillRequirements.Any(req => req > 0 && !character.Spells.Items.Any(s => s.Id == req)))
        {
            return new SkillLearnValidation(SkillLearnFailure.MissingPrerequisite, character.Class);
        }

        if (skill.ClassRequirement > 0 && character.Class != skill.ClassRequirement)
        {
            return new SkillLearnValidation(SkillLearnFailure.WrongClass, skill.ClassRequirement);
        }

        return new SkillLearnValidation(SkillLearnFailure.None);
    }
}
