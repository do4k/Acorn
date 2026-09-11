using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Tests.TestHelpers;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class StatSkillServiceTests
{
    private readonly Ecf _ecf = GameTestFactory.Ecf();

    private static StatSkillService CreateSut(
        int maxStat = 10000,
        int maxSkillLevel = 100,
        int statPerLevel = 3,
        int skillPerLevel = 4)
    {
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif());

        return new StatSkillService(
            new StatCalculator(dataFileRepository),
            GameTestFactory.ServerOptions(maxStat, maxSkillLevel, statPerLevel, skillPerLevel));
    }

    [Fact]
    public void AddStatPoint_WhenPointsAvailable_ShouldIncrementStatAndSpendPoint()
    {
        var character = GameTestFactory.Character(statPoints: 2);
        var sut = CreateSut();

        var outcome = sut.AddStatPoint(character, StatId.Str, _ecf);

        outcome.Result.Should().Be(StatSkillSpendResult.Success);
        character.Str.Should().Be(1);
        character.StatPoints.Should().Be(1);
        character.AdjStr.Should().Be(1, "stats are recalculated after spending");
    }

    [Fact]
    public void AddStatPoint_WhenNoStatPoints_ShouldReturnNoStatPointsAndNotChange()
    {
        var character = GameTestFactory.Character(statPoints: 0);
        var sut = CreateSut();

        var outcome = sut.AddStatPoint(character, StatId.Str, _ecf);

        outcome.Result.Should().Be(StatSkillSpendResult.NoStatPoints);
        character.Str.Should().Be(0);
    }

    [Fact]
    public void AddStatPoint_WhenAtMaxStat_ShouldReturnMaxStatReachedAndNotSpendPoint()
    {
        var character = GameTestFactory.Character(statPoints: 5);
        character.Str = 10;
        var sut = CreateSut(maxStat: 10);

        var outcome = sut.AddStatPoint(character, StatId.Str, _ecf);

        outcome.Result.Should().Be(StatSkillSpendResult.MaxStatReached);
        character.Str.Should().Be(10);
        character.StatPoints.Should().Be(5, "a rejected spend must not consume a point");
    }

    [Fact]
    public void AddStatPoint_WhenInvalidStat_ShouldReturnInvalidStat()
    {
        var character = GameTestFactory.Character(statPoints: 5);
        var sut = CreateSut();

        var outcome = sut.AddStatPoint(character, (StatId)99, _ecf);

        outcome.Result.Should().Be(StatSkillSpendResult.InvalidStat);
        character.StatPoints.Should().Be(5);
    }

    [Fact]
    public void AddSkillPoint_WhenKnownSpell_ShouldLevelUpAndReturnNewLevel()
    {
        var character = GameTestFactory.Character(skillPoints: 2);
        character.Spells.Items.Add(new Acorn.Game.Models.Spell(7, 2));
        var sut = CreateSut();

        var outcome = sut.AddSkillPoint(character, 7);

        outcome.Result.Should().Be(StatSkillSpendResult.Success);
        outcome.SkillLevel.Should().Be(3);
        character.SkillPoints.Should().Be(1);
        character.Spells.Items.Single(s => s.Id == 7).Level.Should().Be(3);
    }

    [Fact]
    public void AddSkillPoint_WhenAtMaxSkillLevel_ShouldReturnMaxSkillLevelReached()
    {
        var character = GameTestFactory.Character(skillPoints: 2);
        character.Spells.Items.Add(new Acorn.Game.Models.Spell(7, 5));
        var sut = CreateSut(maxSkillLevel: 5);

        var outcome = sut.AddSkillPoint(character, 7);

        outcome.Result.Should().Be(StatSkillSpendResult.MaxSkillLevelReached);
        character.SkillPoints.Should().Be(2);
        character.Spells.Items.Single(s => s.Id == 7).Level.Should().Be(5);
    }

    [Fact]
    public void AddSkillPoint_WhenNoSkillPoints_ShouldReturnNoSkillPoints()
    {
        var character = GameTestFactory.Character(skillPoints: 0);
        character.Spells.Items.Add(new Acorn.Game.Models.Spell(7, 0));
        var sut = CreateSut();

        var outcome = sut.AddSkillPoint(character, 7);

        outcome.Result.Should().Be(StatSkillSpendResult.NoSkillPoints);
        character.Spells.Items.Single(s => s.Id == 7).Level.Should().Be(0);
    }

    [Fact]
    public void AddSkillPoint_WhenUnknownSpell_ShouldReturnUnknownSpell()
    {
        var character = GameTestFactory.Character(skillPoints: 2);
        var sut = CreateSut();

        var outcome = sut.AddSkillPoint(character, 7);

        outcome.Result.Should().Be(StatSkillSpendResult.UnknownSpell);
        character.SkillPoints.Should().Be(2);
    }

    [Fact]
    public void Reset_ShouldClearStatsAndSpellsAndReturnLevelPoints()
    {
        var character = GameTestFactory.Character(level: 10, statPoints: 1, skillPoints: 1);
        character.Str = 4;
        character.Int = 2;
        character.Spells.Items.Add(new Acorn.Game.Models.Spell(7, 3));
        var sut = CreateSut(statPerLevel: 3, skillPerLevel: 4);

        sut.Reset(character, _ecf);

        character.Str.Should().Be(0);
        character.Int.Should().Be(0);
        character.Spells.Items.Should().BeEmpty();
        character.StatPoints.Should().Be(30, "level 10 * 3 stat points per level");
        character.SkillPoints.Should().Be(40, "level 10 * 4 skill points per level");
    }
}
