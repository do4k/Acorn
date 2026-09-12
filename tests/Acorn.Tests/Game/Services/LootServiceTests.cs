using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class LootServiceTests
{
    private readonly LootService _sut;

    public LootServiceTests()
    {
        _sut = new LootService();
    }

    [Fact]
    public void RollDrop_WhenNpcHasNoLootTableAndNoGlobalDrops_ShouldReturnNull()
    {
        var result = _sut.RollDrop(npcId: 999);

        result.Should().BeNull();
    }

    [Fact]
    public void RollDrop_WhenNpcHasNoSpecificLootTable_ShouldStillRollGlobalDrops()
    {
        // A 100% global drop should always be returned, even for an NPC with no
        // NPC-specific loot table registered (e.g. a universal gold drop).
        _sut.RegisterGlobalDrops([new LootDrop(itemId: 1, minAmount: 1, maxAmount: 5, ratePercent: 100)]);

        var result = _sut.RollDrop(npcId: 42);

        result.Should().NotBeNull();
        result!.ItemId.Should().Be(1);
    }

    [Fact]
    public void RollDrop_ShouldConsiderBothNpcSpecificAndGlobalDrops()
    {
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops = [new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 100)]
        });
        _sut.RegisterGlobalDrops([new LootDrop(itemId: 1, minAmount: 1, maxAmount: 5, ratePercent: 100)]);

        // Both rates are 100%, so a roll should always return one of the two configured drops.
        var result = _sut.RollDrop(npcId: 1);

        result.Should().NotBeNull();
        new[] { 1, 100 }.Should().Contain(result!.ItemId);
    }

    [Fact]
    public void RollDrop_WhenRateIsZero_ShouldNeverDrop()
    {
        _sut.RegisterGlobalDrops([new LootDrop(itemId: 1, minAmount: 1, maxAmount: 5, ratePercent: 0)]);

        for (var i = 0; i < 50; i++)
        {
            _sut.RollDrop(npcId: 1).Should().BeNull();
        }
    }

    [Fact]
    public void RollDrop_WhenChanceIsTenPercent_ShouldNotAlwaysDrop()
    {
        // Regression: a 10% drop used to become a guaranteed drop because the drop file
        // loader converted the percentage twice (10 -> 100).
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops = [new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 10)]
        });

        var results = Enumerable.Range(0, 200).Select(_ => _sut.RollDrop(npcId: 1)).ToList();

        results.Should().Contain(r => r == null, "90% of rolls must miss");
        results.Should().Contain(r => r != null, "10% of rolls must hit");
        results.Count(r => r != null).Should().BeInRange(2, 60,
            "a 10% chance over 200 rolls should land near 20, not 200");
    }

    [Fact]
    public void RollDrop_WhenChancesSumTo100_ShouldAlwaysDrop()
    {
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops =
            [
                new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 60),
                new LootDrop(itemId: 200, minAmount: 1, maxAmount: 1, ratePercent: 40)
            ]
        });

        for (var i = 0; i < 100; i++)
        {
            _sut.RollDrop(npcId: 1).Should().NotBeNull();
        }
    }

    [Fact]
    public void RollDrop_WhenChancesExceed100_ShouldScaleProportionally()
    {
        // 150 + 50 = 200 scales the entries to 75% / 25% and guarantees a drop, matching
        // eoserv's default DropRateMode 3.
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops =
            [
                new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 150),
                new LootDrop(itemId: 200, minAmount: 1, maxAmount: 1, ratePercent: 50)
            ]
        });

        var results = Enumerable.Range(0, 1000).Select(_ => _sut.RollDrop(npcId: 1)).ToList();

        results.Should().NotContainNulls();
        results.Count(r => r!.ItemId == 100).Should()
            .BeGreaterThan(results.Count(r => r!.ItemId == 200), "the 150 entry is three times as likely");
    }

    [Fact]
    public void RollDrop_ShouldRollNpcAndGlobalDropsInOneWeightedRoll()
    {
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops = [new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 50)]
        });
        _sut.RegisterGlobalDrops([new LootDrop(itemId: 1, minAmount: 1, maxAmount: 5, ratePercent: 50)]);

        for (var i = 0; i < 100; i++)
        {
            var result = _sut.RollDrop(npcId: 1);
            result.Should().NotBeNull("the combined chances add up to 100");
            new[] { 1, 100 }.Should().Contain(result!.ItemId);
        }
    }

    [Fact]
    public void RollDropAmount_ShouldReturnValueWithinRange()
    {
        var drop = new LootDrop(itemId: 1, minAmount: 3, maxAmount: 7, ratePercent: 100);

        for (var i = 0; i < 50; i++)
        {
            var amount = _sut.RollDropAmount(drop);
            amount.Should().BeInRange(3, 7);
        }
    }

    [Fact]
    public void Seal_ThenRegisterNpcLootTable_ShouldThrow()
    {
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops = [new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 100)]
        });
        _sut.Seal();

        var act = () => _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 2,
            Drops = [new LootDrop(itemId: 200, minAmount: 1, maxAmount: 1, ratePercent: 100)]
        });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Seal_StillAllowsLookupAndRolls()
    {
        _sut.RegisterNpcLootTable(new NpcLootTable
        {
            NpcId = 1,
            Drops = [new LootDrop(itemId: 100, minAmount: 1, maxAmount: 1, ratePercent: 100)]
        });
        _sut.Seal();

        _sut.GetNpcLootTable(1).Should().NotBeNull();
        _sut.RollDrop(npcId: 1).Should().NotBeNull();
    }
}
