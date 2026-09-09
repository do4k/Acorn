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
