using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Game.Services;

public class TradeServiceTests
{
    private const int LightItemId = 1;  // weight 5
    private const int MediumItemId = 2; // weight 10
    private const int HeavyItemId = 3;  // weight 50

    private readonly TradeService _sut;

    public TradeServiceTests()
    {
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif
        {
            Items = new List<EifRecord>
            {
                new() { Name = "Light", Weight = 5 },
                new() { Name = "Medium", Weight = 10 },
                new() { Name = "Heavy", Weight = 50 }
            }
        });

        _sut = new TradeService(new InventoryService(), new WeightCalculator(), dataFileRepository);
    }

    private static Character CreateCharacter(int maxWeight = 1000, params (int ItemId, int Amount)[] items)
    {
        var character = new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            MaxWeight = maxWeight,
            Inventory = new Inventory([]),
            Bank = new Bank([]),
            Paperdoll = new Paperdoll(),
            Spells = new Spells([])
        };

        foreach (var (itemId, amount) in items)
        {
            character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        }

        return character;
    }

    private static int AmountOf(Character character, int itemId)
        => character.Inventory.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;

    [Test]
    public void TryCompleteTrade_WhenBothSidesHoldOffers_ShouldSwapItems()
    {
        // Arrange
        var player = CreateCharacter(1000, (LightItemId, 5));
        var partner = CreateCharacter(1000, (MediumItemId, 3));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(LightItemId, 5)],
            partner, [new TradeOffer(MediumItemId, 3)]);

        // Assert
        result.Success.Should().BeTrue();
        AmountOf(player, LightItemId).Should().Be(0);
        AmountOf(player, MediumItemId).Should().Be(3);
        AmountOf(partner, MediumItemId).Should().Be(0);
        AmountOf(partner, LightItemId).Should().Be(5);
    }

    [Test]
    public void TryCompleteTrade_WhenSwappingSameItem_ShouldTransferStacks()
    {
        // Arrange
        var player = CreateCharacter(1000, (MediumItemId, 10));
        var partner = CreateCharacter(1000, (MediumItemId, 4));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(MediumItemId, 10)],
            partner, [new TradeOffer(MediumItemId, 4)]);

        // Assert
        result.Success.Should().BeTrue();
        AmountOf(player, MediumItemId).Should().Be(4);
        AmountOf(partner, MediumItemId).Should().Be(10);
    }

    [Test]
    public void TryCompleteTrade_WhenPlayerMissingOfferedItems_ShouldFailAndNotMutateInventories()
    {
        // Arrange - player only holds 5 but offers 10
        var player = CreateCharacter(1000, (LightItemId, 5));
        var partner = CreateCharacter(1000, (MediumItemId, 3));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(LightItemId, 10)],
            partner, [new TradeOffer(MediumItemId, 3)]);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(TradeCompletionStatus.PlayerMissingItems);
        AmountOf(player, LightItemId).Should().Be(5);
        AmountOf(player, MediumItemId).Should().Be(0);
        AmountOf(partner, MediumItemId).Should().Be(3);
        AmountOf(partner, LightItemId).Should().Be(0);
    }

    [Test]
    public void TryCompleteTrade_WhenPartnerMissingOfferedItems_ShouldFailAndNotMutateInventories()
    {
        // Arrange - partner only holds 1 but offers 3
        var player = CreateCharacter(1000, (LightItemId, 5));
        var partner = CreateCharacter(1000, (MediumItemId, 1));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(LightItemId, 5)],
            partner, [new TradeOffer(MediumItemId, 3)]);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(TradeCompletionStatus.PartnerMissingItems);
        AmountOf(player, LightItemId).Should().Be(5);
        AmountOf(partner, MediumItemId).Should().Be(1);
    }

    [Test]
    public void TryCompleteTrade_WhenPlayerCannotCarryReceivedItems_ShouldFailAndNotMutateInventories()
    {
        // Arrange - player is near capacity and would go over by receiving a heavy item
        var player = CreateCharacter(90, (HeavyItemId, 1));
        var partner = CreateCharacter(1000, (HeavyItemId, 1));

        // Act - player gives away nothing, receives 50 weight (50 + 50 > 90)
        var result = _sut.TryCompleteTrade(
            player, [],
            partner, [new TradeOffer(HeavyItemId, 1)]);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(TradeCompletionStatus.PlayerOverweight);
        AmountOf(player, HeavyItemId).Should().Be(1);
        AmountOf(partner, HeavyItemId).Should().Be(1);
    }

    [Test]
    public void TryCompleteTrade_WhenPartnerCannotCarryReceivedItems_ShouldFailAndNotMutateInventories()
    {
        // Arrange
        var player = CreateCharacter(1000, (HeavyItemId, 1));
        var partner = CreateCharacter(90, (HeavyItemId, 1));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(HeavyItemId, 1)],
            partner, []);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(TradeCompletionStatus.PartnerOverweight);
        AmountOf(player, HeavyItemId).Should().Be(1);
        AmountOf(partner, HeavyItemId).Should().Be(1);
    }

    [Test]
    public void TryCompleteTrade_WhenGivingAwayItemsFreesEnoughWeight_ShouldSucceed()
    {
        // Arrange - player is at capacity with a heavy item and swaps it for a light one
        var player = CreateCharacter(50, (HeavyItemId, 1));
        var partner = CreateCharacter(1000, (LightItemId, 1));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(HeavyItemId, 1)],
            partner, [new TradeOffer(LightItemId, 1)]);

        // Assert
        result.Success.Should().BeTrue();
        AmountOf(player, HeavyItemId).Should().Be(0);
        AmountOf(player, LightItemId).Should().Be(1);
        AmountOf(partner, HeavyItemId).Should().Be(1);
        AmountOf(partner, LightItemId).Should().Be(0);
    }

    [Test]
    public void TryCompleteTrade_WhenExceedingWeightLimitExactly_ShouldSucceed()
    {
        // Arrange - 95 weight carried + 5 received = 100 == MaxWeight
        var player = CreateCharacter(100, (MediumItemId, 9), (LightItemId, 1));
        var partner = CreateCharacter(1000, (LightItemId, 1));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [],
            partner, [new TradeOffer(LightItemId, 1)]);

        // Assert
        result.Success.Should().BeTrue();
        AmountOf(player, LightItemId).Should().Be(2);
    }

    [Test]
    public void TryCompleteTrade_WhenMultipleItemsOffered_ShouldSwapAll()
    {
        // Arrange
        var player = CreateCharacter(1000, (LightItemId, 2), (MediumItemId, 3));
        var partner = CreateCharacter(1000, (HeavyItemId, 1));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(LightItemId, 2), new TradeOffer(MediumItemId, 3)],
            partner, [new TradeOffer(HeavyItemId, 1)]);

        // Assert
        result.Success.Should().BeTrue();
        AmountOf(player, LightItemId).Should().Be(0);
        AmountOf(player, MediumItemId).Should().Be(0);
        AmountOf(player, HeavyItemId).Should().Be(1);
        AmountOf(partner, HeavyItemId).Should().Be(0);
        AmountOf(partner, LightItemId).Should().Be(2);
        AmountOf(partner, MediumItemId).Should().Be(3);
    }

    [Test]
    public void TryCompleteTrade_WhenOfferAmountIsZero_ShouldFail()
    {
        // Arrange
        var player = CreateCharacter(1000, (LightItemId, 5));
        var partner = CreateCharacter(1000, (MediumItemId, 3));

        // Act
        var result = _sut.TryCompleteTrade(
            player, [new TradeOffer(LightItemId, 0)],
            partner, [new TradeOffer(MediumItemId, 3)]);

        // Assert
        result.Success.Should().BeFalse();
        result.Status.Should().Be(TradeCompletionStatus.PlayerMissingItems);
    }
}