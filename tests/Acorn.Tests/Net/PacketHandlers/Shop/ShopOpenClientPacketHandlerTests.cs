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

    private static ShopTestSupport.Fixture CreateFixture(ShopData? shop = null, int playerX = 11)
    {
        return ShopTestSupport.Create(
            shop ?? ShopTestSupport.BuildShop(
                trades: [new TradeItem(TradeItemId, BuyPrice: 10, SellPrice: 2, MaxAmount: 99)],
                crafts: [new CraftItem(CraftItemId, [new ShopCraftIngredient(IngredientItemId, 2)])]),
            items: [(TradeItemId, 3), (CraftItemId, 10), (IngredientItemId, 1)],
            playerX: playerX);
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
    public async Task Open_WhenNpcOutsideClientView_ShouldNotReply()
    {
        // Arrange - 20 Manhattan tiles away, beyond the client's own 11/14-tile cull range
        var fixture = CreateFixture();
        fixture.Character.X = 20;
        fixture.Character.Y = 20;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        fixture.Communicator.Sent.Should().BeEmpty("a client cannot see or click the NPC at that distance");
        fixture.Player.InteractingNpcIndex.Should().BeNull();
    }

    [Test]
    public async Task Open_WhenNpcInViewButNotAdjacent_ShouldSendShopWindow()
    {
        // Arrange - clients send Shop/Open immediately when the NPC sprite is clicked,
        // without walking first, so any visible vendor must be openable from anywhere.
        var fixture = CreateFixture(playerX: 16);

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, OpenPacket());

        // Assert
        ShopTestSupport.DecodeLastSent(fixture).Should().BeOfType<ShopOpenServerPacket>();
        fixture.Player.InteractingNpcIndex.Should().Be(ShopTestSupport.NpcIndex,
            "clicking a vendor from inside the client view starts the interaction");
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
