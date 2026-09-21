using Acorn.Data;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Shop;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using NSubstitute;

namespace Acorn.Tests.Net.PacketHandlers.Shop;

public class ShopCreateClientPacketHandlerTests
{
    private const int CraftItemId = 5;
    private const int IngredientItemId = 6;
    private const int FillerItemId = 7;

    private static ShopTestSupport.Fixture CreateFixture(IInventoryService? inventoryOverride = null)
    {
        return ShopTestSupport.Create(
            ShopTestSupport.BuildShop(
                crafts: [new ShopCraftItem(CraftItemId, [new ShopCraftIngredient(IngredientItemId, 2)])]),
            items: [(CraftItemId, 10), (IngredientItemId, 1), (FillerItemId, 1)],
            inventoryOverride: inventoryOverride);
    }

    private static ShopCreateClientPacketHandler CreateHandler(ShopTestSupport.Fixture fixture)
    {
        return new ShopCreateClientPacketHandler(
            NullLogger<ShopCreateClientPacketHandler>.Instance,
            fixture.DataFiles,
            fixture.Shops,
            fixture.Inventory);
    }

    private static ShopCreateClientPacket CraftPacket()
    {
        return new ShopCreateClientPacket { CraftItemId = CraftItemId };
    }

    [Test]
    public async Task Create_HappyPath_ShouldConsumeIngredientsAndGrantItem()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(IngredientItemId, 2);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, CraftPacket());

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, IngredientItemId).Should().Be(0);
        fixture.Inventory.GetItemAmount(fixture.Character, CraftItemId).Should().Be(1);
        fixture.Communicator.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task Create_MissingIngredients_ShouldNotTransact()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Character.GiveItem(IngredientItemId, 1);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, CraftPacket());

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, IngredientItemId).Should().Be(1);
        fixture.Inventory.GetItemAmount(fixture.Character, CraftItemId).Should().Be(0);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Create_WhenProductWouldExceedWeight_ShouldKeepIngredients()
    {
        // Arrange - 98 fillers + 2 ingredients weigh 100/100; the 10-weight product cannot fit
        var fixture = CreateFixture();
        fixture.Character.GiveItem(FillerItemId, 98);
        fixture.Character.GiveItem(IngredientItemId, 2);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, CraftPacket());

        // Assert
        fixture.Inventory.GetItemAmount(fixture.Character, IngredientItemId).Should().Be(2);
        fixture.Inventory.GetItemAmount(fixture.Character, CraftItemId).Should().Be(0);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Create_WhenItemCannotBeAdded_ShouldRefundIngredients()
    {
        // Arrange - inventory service that accepts removals but refuses to hand out the product
        var inventory = Substitute.For<IInventoryService>();
        inventory.GetItemAmount(Arg.Any<Character>(), IngredientItemId).Returns(2);
        inventory.CanHoldItem(Arg.Any<Character>(), Arg.Any<Moffat.EndlessOnline.SDK.Protocol.Pub.Eif>(), CraftItemId, 1)
            .Returns(true);
        inventory.TryRemoveItem(Arg.Any<Character>(), IngredientItemId, 2).Returns(true);
        inventory.TryAddItem(Arg.Any<Character>(), CraftItemId, 1).Returns(false);

        var fixture = CreateFixture(inventoryOverride: inventory);
        fixture.Player.InteractingNpcIndex = ShopTestSupport.NpcIndex;

        // Act
        await CreateHandler(fixture).HandleAsync(fixture.Player, CraftPacket());

        // Assert
        inventory.Received(1).TryAddItem(Arg.Any<Character>(), IngredientItemId, 2);
        fixture.Communicator.Sent.Should().BeEmpty("no craft reply is sent after a refund");
    }
}
