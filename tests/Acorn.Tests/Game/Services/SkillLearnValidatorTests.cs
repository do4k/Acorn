using Acorn.Data;
using Acorn.Game.Services;
using Acorn.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class SkillLearnValidatorTests
{
    private const int SpellId = 7;

    private static SkillMasterSkill Skill(
        int price = 0,
        int levelRequirement = 0,
        int classRequirement = 0,
        List<int>? skillRequirements = null,
        int strRequirement = 0)
    {
        return new SkillMasterSkill(
            SpellId,
            levelRequirement,
            classRequirement,
            price,
            skillRequirements ?? [0, 0, 0, 0],
            strRequirement,
            0,
            0,
            0,
            0,
            0);
    }

    [Fact]
    public void Validate_WhenAllRequirementsMet_ShouldAllowLearning()
    {
        var character = GameTestFactory.Character();

        var result = SkillLearnValidator.Validate(character, Skill(price: 100), goldAmount: 100);

        result.CanLearn.Should().BeTrue();
        result.Failure.Should().Be(SkillLearnFailure.None);
    }

    [Fact]
    public void Validate_WhenAlreadyKnown_ShouldReturnAlreadyKnown()
    {
        var character = GameTestFactory.Character();
        character.Spells.Items.Add(new Acorn.Game.Models.Spell(SpellId, 1));

        var result = SkillLearnValidator.Validate(character, Skill(), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.AlreadyKnown);
    }

    [Fact]
    public void Validate_WhenNotEnoughGold_ShouldReturnInsufficientGoldWithPlayerClass()
    {
        var character = GameTestFactory.Character();

        var result = SkillLearnValidator.Validate(character, Skill(price: 100), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.InsufficientGold);
        result.WrongClassId.Should().Be(character.Class);
    }

    [Fact]
    public void Validate_WhenLevelTooLow_ShouldReturnLevelTooLow()
    {
        var character = GameTestFactory.Character();

        var result = SkillLearnValidator.Validate(character, Skill(levelRequirement: 5), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.LevelTooLow);
    }

    [Fact]
    public void Validate_WhenStatsTooLow_ShouldReturnStatsTooLow()
    {
        var character = GameTestFactory.Character();

        var result = SkillLearnValidator.Validate(character, Skill(strRequirement: 10), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.StatsTooLow);
    }

    [Fact]
    public void Validate_WhenMissingPrerequisite_ShouldReturnMissingPrerequisite()
    {
        var character = GameTestFactory.Character();

        var result = SkillLearnValidator.Validate(
            character, Skill(skillRequirements: [3, 0, 0, 0]), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.MissingPrerequisite);
    }

    [Fact]
    public void Validate_WhenWrongClass_ShouldReturnWrongClassWithRequiredClass()
    {
        var character = GameTestFactory.Character();
        character.Class = 1;

        var result = SkillLearnValidator.Validate(character, Skill(classRequirement: 2), goldAmount: 0);

        result.Failure.Should().Be(SkillLearnFailure.WrongClass);
        result.WrongClassId.Should().Be(2, "the client shows the required class in its message");
    }
}
