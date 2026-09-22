using Acorn.Data;
using Acorn.Game.Services;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers.Shop;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using NSubstitute;

namespace Acorn.Tests.Net.PacketHandlers.Shop;

public class ShopBuyClientPacketHandlerTests
{
    private const int TradeItemId = 4;
    private const int TradeWeight = 3;

    private static ShopTestSupport.Fixture CreateFixture(ShopData? shop = null, IInventoryService? inventoryOverride = null)
    {
        return ShopTestSupport.Create(
            shop ?? ShopTestSupport.BuildShop(
                trades: [new ShopTradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)]),
            items: [(TradeItemId, TradeWeight)],
            inventoryOverride: inventoryOverride);
    }

    private static ShopBuyClientPacketHandler CreateHandler(ShopTestSupport.Fixture fixture)
    {
        return new ShopBuyClientPacketHandler(
            NullLogger<ShopBuyClientPacketHandler>.Instance,
            fixture.DataFiles,
            fixture.Shops,
            fixture.Inventory,
            new AcornMetrics());
    }

    private static ShopBuyClientPacket BuyPacket(int amount)
    {
        return new ShopBuyClientPacket
        {
            BuyItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item { Id = TradeItemId, Amount = amount }
        };
    }

    [Test]
    public async Task Buy_HappyPath_ShouldTransferGoldAndItemAndReply()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(10);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(900);
        fixture.Communicator.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task Buy_WhenNpcInViewButNotAdjacent_ShouldComplete()
    {
        // Arrange - the shop window was opened by clicking the vendor from across the
        // store; buying from the same spot must work.
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;
        fixture.Character.X = 16;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(10);
        fixture.Communicator.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task Buy_LimitedByWeight_ShouldBuyOnlyWhatFits()
    {
        // Arrange - 100 max weight, item weighs 3 => at most 33 fit
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 100000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(50));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(33);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(100000 - 33 * 10);
    }

    [Test]
    public async Task Buy_LimitedByShopMaxAmount_ShouldBuyOnlyUpToMax()
    {
        // Arrange
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new ShopTradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 5)]));
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(5);
    }

    [Test]
    public async Task Buy_WithoutEnoughGold_ShouldNotTransact()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 25);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(0);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(25);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Buy_ItemNotOfferedByShop_ShouldNotTransact()
    {
        // Arrange - trade exists only for selling (buy price 0)
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new ShopTradeItem(TradeItemId, BuyPrice: 0, SellPrice: 2, MaxAmount: 99)]));
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(0);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Buy_CostOverflowsItemCap_ShouldNotTransact()
    {
        // Arrange - 1.5 billion gold per unit; 10 units is far beyond the 2 billion cap
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new ShopTradeItem(TradeItemId, BuyPrice: 1_500_000_000, SellPrice: 0, MaxAmount: 99)]));
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 2_000_000_000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(10));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(0);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(2_000_000_000);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Buy_InvalidAmount_ShouldNotTransact()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(0));

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Buy_WhenPlayerMovedOutOfNpcView_ShouldNotTransact()
    {
        // Arrange - interaction started while the vendor was visible, the player then
        // teleported 20 Manhattan tiles away, outside the client's own cull range
        var fixture = CreateFixture();
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, 1000);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;
        fixture.Character.X = 20;
        fixture.Character.Y = 20;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(1));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(0);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(1000);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Buy_WhenItemCannotBeAdded_ShouldRefundGold()
    {
        // Arrange - inventory service that accepts gold removal but refuses the item add
        var inventory = Substitute.For<IInventoryService>();
        inventory.GetItemAmount(Arg.Any<Acorn.Game.Models.Character>(), ShopTestSupport.GoldItemId).Returns(1000);
        inventory.TryRemoveItem(Arg.Any<Acorn.Game.Models.Character>(), ShopTestSupport.GoldItemId, 10).Returns(true);
        inventory.TryAddItem(Arg.Any<Acorn.Game.Models.Character>(), TradeItemId, 1).Returns(false);

        var fixture = CreateFixture(inventoryOverride: inventory);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, BuyPacket(1));

        // Assert
        inventory.Received(1).TryAddItem(
            Arg.Any<Acorn.Game.Models.Character>(), ShopTestSupport.GoldItemId, 10);
        fixture.Communicator.Sent.Should().BeEmpty("no purchase reply is sent after a refund");
    }
}
