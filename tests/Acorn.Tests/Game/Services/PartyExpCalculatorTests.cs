using Acorn.World.Services.Party;
using FluentAssertions;

namespace Acorn.Tests.Game.Services;

public class PartyExpCalculatorTests
{
    [Test]
    public void CalculateShare_EqualMode_WhenExpDoesNotDivideEvenly_ShouldRoundUp()
    {
        // ceil(100 / 3) = 34
        var result = PartyExpCalculator.CalculateShare(100, memberLevel: 5, sumOfLevels: 15, memberCount: 3,
            PartyShareMode.Equal);

        result.Should().Be(34);
    }

    [Test]
    public void CalculateShare_EqualMode_WhenExpDividesEvenly_ShouldSplitExactly()
    {
        var result = PartyExpCalculator.CalculateShare(100, memberLevel: 5, sumOfLevels: 10, memberCount: 2,
            PartyShareMode.Equal);

        result.Should().Be(50);
    }

    [Test]
    public void CalculateShare_LevelBasedMode_ShouldWeightByLevel()
    {
        // level 10 of a total of 11 levels gets ceil(110 * 10 / 11) = 100
        var high = PartyExpCalculator.CalculateShare(110, memberLevel: 10, sumOfLevels: 11, memberCount: 2,
            PartyShareMode.LevelBased);

        high.Should().Be(100);
    }

    [Test]
    public void CalculateShare_LevelBasedMode_WhenMemberIsLevelZero_ShouldTreatAsLevelOne()
    {
        // level 0 counts as 1 of a total of 11 levels -> ceil(110 * 1 / 11) = 10
        var low = PartyExpCalculator.CalculateShare(110, memberLevel: 0, sumOfLevels: 11, memberCount: 2,
            PartyShareMode.LevelBased);

        low.Should().Be(10);
    }

    [Test]
    [Arguments(0, 5, 5, 1)]
    [Arguments(100, 5, 5, 0)]
    [Arguments(100, 5, 0, 1)]
    [Arguments(-50, 5, 5, 1)]
    public void CalculateShare_WhenInputsAreInvalid_ShouldReturnZero(int totalExp, int level, int sumOfLevels,
        int memberCount)
    {
        var result = PartyExpCalculator.CalculateShare(totalExp, level, sumOfLevels, memberCount,
            PartyShareMode.Equal);

        result.Should().Be(0);
    }
}