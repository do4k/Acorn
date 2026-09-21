using Acorn.Data;
using Acorn.Game.Models;
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

public class ShopSellClientPacketHandlerTests
{
    private const int TradeItemId = 4;
    private const int TradeWeight = 3;
    private const int MaxGold = 2_000_000_000;

    private static ShopTestSupport.Fixture CreateFixture(ShopData? shop = null, IInventoryService? inventoryOverride = null)
    {
        return ShopTestSupport.Create(
            shop ?? ShopTestSupport.BuildShop(
                trades: [new ShopTradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)]),
            items: [(TradeItemId, TradeWeight)],
            inventoryOverride: inventoryOverride);
    }

    private static ShopSellClientPacketHandler CreateHandler(ShopTestSupport.Fixture fixture)
    {
        return new ShopSellClientPacketHandler(
            NullLogger<ShopSellClientPacketHandler>.Instance,
            fixture.DataFiles,
            fixture.Shops,
            fixture.Inventory,
            new AcornMetrics());
    }

    private static ShopSellClientPacket SellPacket(int amount)
    {
        return new ShopSellClientPacket
        {
            SellItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item { Id = TradeItemId, Amount = amount }
        };
    }

    [Test]
    public async Task Sell_HappyPath_ShouldTransferItemAndGoldAndReply()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(TradeItemId, 5);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(0);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(10);
        fixture.Communicator.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task Sell_MoreThanOwned_ShouldNotTransact()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(TradeItemId, 3);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(3);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(0);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Sell_ItemNotWantedByShop_ShouldNotTransact()
    {
        // Arrange - trade exists only for buying (sell price 0)
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new ShopTradeItem(TradeItemId, BuyPrice: 10, SellPrice: 0, MaxAmount: 99)]));
        fixture.Character.GiveItem(TradeItemId, 5);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(5);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Sell_WhenPayoutWouldExceedGoldCap_ShouldSellOnlyWhatFits()
    {
        // Arrange - gold stack leaves room for 4 more gold, payout is 2 per item
        var fixture = CreateFixture();
        fixture.Character.GiveItem(TradeItemId, 5);
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, MaxGold - 4);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert - two items sold for exactly the remaining cap, three stay owned
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(3);
        fixture.Inventory.GetItemAmount(fixture.Character, ShopTestSupport.GoldItemId).Should().Be(MaxGold);
        fixture.Communicator.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task Sell_WhenGoldStackFull_ShouldNotTransact()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(TradeItemId, 5);
        fixture.Character.GiveItem(ShopTestSupport.GoldItemId, MaxGold);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, TradeItemId).Should().Be(5);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Sell_WhenGoldCannotBeAdded_ShouldReturnSoldItems()
    {
        // Arrange - inventory service that removes the item but refuses to pay gold
        var inventory = Substitute.For<IInventoryService>();
        inventory.GetItemAmount(Arg.Any<Character>(), TradeItemId).Returns(5);
        inventory.TryRemoveItem(Arg.Any<Character>(), TradeItemId, 5).Returns(true);
        inventory.TryAddItem(Arg.Any<Character>(), ShopTestSupport.GoldItemId, 10).Returns(false);

        var fixture = CreateFixture(inventoryOverride: inventory);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, SellPacket(5));

        // Assert
        inventory.Received(1).TryAddItem(Arg.Any<Character>(), TradeItemId, 5);
        fixture.Communicator.Sent.Should().BeEmpty("no sale reply is sent after a refund");
    }
}
