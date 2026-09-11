using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Tests.TestHelpers;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class FormulaServiceTests
{
    private readonly Ecf _ecf = GameTestFactory.Ecf();

    private static FormulaService CreateSut(int statPerLevel = 3, int skillPerLevel = 4)
    {
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif());

        return new FormulaService(
            new StatCalculator(dataFileRepository),
            GameTestFactory.ServerOptions(statPerLevel: statPerLevel, skillPerLevel: skillPerLevel));
    }

    [Fact]
    public void LevelUp_WhenEnoughExperience_ShouldGrantDefaultPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 100_000;
        var sut = CreateSut();

        var level = sut.LevelUp(character, _ecf);

        level.Should().Be(1);
        character.StatPoints.Should().Be(3, "default StatPerLevel is 3");
        character.SkillPoints.Should().Be(4, "default SkillPerLevel is 4 (matches eoserv)");
    }

    [Fact]
    public void LevelUp_ShouldGrantConfiguredPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 100_000;
        var sut = CreateSut(statPerLevel: 5, skillPerLevel: 2);

        sut.LevelUp(character, _ecf);

        character.StatPoints.Should().Be(5);
        character.SkillPoints.Should().Be(2);
    }

    [Fact]
    public void LevelUp_WhenNotEnoughExperience_ShouldNotLevelOrGrantPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 0;
        var sut = CreateSut();

        var level = sut.LevelUp(character, _ecf);

        level.Should().Be(0);
        character.StatPoints.Should().Be(0);
        character.SkillPoints.Should().Be(0);
    }
}
