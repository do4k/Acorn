using System.Collections.Concurrent;
using Acorn.Database.Models;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Xunit;
using Character = Acorn.Game.Models.Character;
using Inventory = Acorn.Game.Models.Inventory;
using Bank = Acorn.Game.Models.Bank;

namespace Acorn.Tests.Game.Models;

public class CharacterTests
{
    private static Character CreateTestCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            X = 10,
            Y = 20,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll
            {
                Hat = 1,
                Armor = 2,
                Boots = 3,
                Weapon = 4,
                Shield = 5,
                Necklace = 6,
                Belt = 7,
                Gloves = 8,
                Accessory = 9,
                Ring1 = 10,
                Ring2 = 11,
                Armlet1 = 12,
                Armlet2 = 13,
                Bracer1 = 14,
                Bracer2 = 15
            },
            Spells = new Acorn.Game.Models.Spells([])
        };
    }

    [Fact]
    public void Items_ShouldReturnProjectedInventoryItems()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });
        character.Inventory.Items.Add(new ItemWithAmount { Id = 2, Amount = 5 });

        // Act
        var items = character.Items().ToList();

        // Assert
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.Id == 1 && i.Amount == 10);
        items.Should().Contain(i => i.Id == 2 && i.Amount == 5);
    }

    [Fact]
    public void AsCoords_ShouldReturnCoordsWithXAndY()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.X = 15;
        character.Y = 25;

        // Act
        var coords = character.AsCoords();

        // Assert
        coords.X.Should().Be(15);
        coords.Y.Should().Be(25);
    }

    [Fact]
    public void Equipment_ShouldReturnPaperdollAsEquipmentPaperdoll()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var equipment = character.Equipment();

        // Assert
        equipment.Hat.Should().Be(1);
        equipment.Armor.Should().Be(2);
        equipment.Boots.Should().Be(3);
        equipment.Weapon.Should().Be(4);
        equipment.Shield.Should().Be(5);
        equipment.Necklace.Should().Be(6);
        equipment.Belt.Should().Be(7);
        equipment.Gloves.Should().Be(8);
        equipment.Accessory.Should().Be(9);
        equipment.Ring.Should().BeEquivalentTo(new[] { 10, 11 });
        equipment.Armlet.Should().BeEquivalentTo(new[] { 12, 13 });
        equipment.Bracer.Should().BeEquivalentTo(new[] { 14, 15 });
    }
}
