using Acorn.Data;
using Acorn.Net.PacketHandlers.Shop;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using CraftItem = Acorn.Data.ShopCraftItem;
using TradeItem = Acorn.Data.ShopTradeItem;

namespace Acorn.Tests.Net.PacketHandlers.Shop;

public class ShopOpenClientPacketHandlerTests
{
    private const int TradeItemId = 4;
    private const int CraftItemId = 5;
    private const int IngredientItemId = 6;

    private static ShopOpenClientPacketHandler CreateHandler(ShopTestSupport.Fixture fixture)
    {
        return new ShopOpenClientPacketHandler(
            NullLogger<ShopOpenClientPacketHandler>.Instance,
            fixture.Shops);
    }

    private static ShopOpenClientPacket OpenPacket()
    {
        return new ShopOpenClientPacket { NpcIndex = ShopTestSupport.NpcIndex };
    }

    private static ShopTestSupport.Fixture CreateFixture(ShopData? shop = null)
    {
        return ShopTestSupport.Create(
            shop ?? ShopTestSupport.BuildShop(
                trades: [new TradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)],
                crafts: [new CraftItem(CraftItemId, [new ShopCraftIngredient(IngredientItemId, 2)])]),
            items: [(TradeItemId, 3), (CraftItemId, 10), (IngredientItemId, 1)]);
    }

    [Test]
    public async Task Open_WhenStandingNextToShop_ShouldSendTradesAndPaddedCrafts()
    {
        // Arrange
        var fixture = CreateFixture();

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        var packet = ShopTestSupport.DecodeLastSent(fixture).Should().BeOfType<ShopOpenServerPacket>().Subject;
        packet.ShopName.Should().Be("Test Shop");
        packet.TradeItems.Should().ContainSingle()
            .Which.ItemId.Should().Be(TradeItemId);
        packet.TradeItems[0].BuyPrice.Should().Be(10);
        packet.TradeItems[0].SellPrice.Should().Be(2);
        packet.TradeItems[0].MaxBuyAmount.Should().Be(99);

        packet.CraftItems.Should().ContainSingle();
        packet.CraftItems[0].ItemId.Should().Be(CraftItemId);
        packet.CraftItems[0].Ingredients.Should().HaveCount(4, "the protocol pads ingredient slots to 4");
        packet.CraftItems[0].Ingredients[0].Id.Should().Be(IngredientItemId);
        packet.CraftItems[0].Ingredients[0].Amount.Should().Be(2);
        packet.CraftItems[0].Ingredients[1].Id.Should().Be(0);
        fixture.Player.InteractingNpcIndex.Should().Be(ShopTestSupport.NpcIndex, "opening starts the interaction");
    }

    [Test]
    public async Task Open_WhenNoShopDataConfigured_ShouldNotReply()
    {
        // Arrange
        var fixture = ShopTestSupport.Create();

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Open_WhenPlayerTooFarFromNpc_ShouldNotReply()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.X = 20;
        fixture.Character.Y = 20;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty("interaction must happen next to the shop NPC");
    }

    [Test]
    public async Task Open_WhenPlayerBelowMinLevel_ShouldNotReply()
    {
        // Arrange
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new TradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)],
            minLevel: 10));
        fixture.Character.Level = 5;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Open_WhenPlayerAboveMaxLevel_ShouldNotReply()
    {
        // Arrange
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new TradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)],
            maxLevel: 5));
        fixture.Character.Level = 10;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Open_WhenPlayerClassDoesNotMatch_ShouldNotReply()
    {
        // Arrange
        var fixture = CreateFixture(ShopTestSupport.BuildShop(
            trades: [new TradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)],
            classRequirement: 2));
        fixture.Character.Class = 1;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty();
    }
}
