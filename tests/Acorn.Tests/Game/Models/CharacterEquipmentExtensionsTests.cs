using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Models;

public class CharacterEquipmentExtensionsTests
{
    private static Character CreateTestCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells([])
        };
    }

    private static IDataFileRepository CreateRepository(params (int Id, ItemSpecial Special)[] items)
    {
        var specials = items.ToDictionary(i => i.Id, i => i.Special);
        var maxId = specials.Count == 0 ? 0 : specials.Keys.Max();

        var eifItems = new List<EifRecord>();
        for (var id = 1; id <= maxId; id++)
        {
            eifItems.Add(new EifRecord
            {
                Name = $"Item{id}",
                Special = specials.TryGetValue(id, out var special) ? special : ItemSpecial.Normal
            });
        }

        var repository = Substitute.For<IDataFileRepository>();
        repository.Eif.Returns(new Eif { Items = eifItems });
        return repository;
    }

    [Fact]
    public void Unequip_WhenItemIsCursed_ShouldRefuseAndKeepItemEquipped()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Paperdoll.Hat = 1;
        var repository = CreateRepository((1, ItemSpecial.Cursed));

        // Act
        var result = character.Unequip(itemId: 1, subLoc: 0, itemDb: repository);

        // Assert
        result.Should().BeFalse();
        character.Paperdoll.Hat.Should().Be(1);
        character.Inventory.Items.Should().BeEmpty();
    }

    [Fact]
    public void Unequip_WhenItemIsNotCursed_ShouldMoveItemToInventory()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Paperdoll.Armor = 2;
        var repository = CreateRepository((2, ItemSpecial.Normal));

        // Act
        var result = character.Unequip(itemId: 2, subLoc: 0, itemDb: repository);

        // Assert
        result.Should().BeTrue();
        character.Paperdoll.Armor.Should().Be(0);
        character.Inventory.Items.Should().ContainSingle(i => i.Id == 2 && i.Amount == 1);
    }

    [Fact]
    public void RemoveCursedEquipment_WhenCursedItemsEquipped_ShouldClearThemAndReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Paperdoll.Hat = 1; // cursed
        character.Paperdoll.Armor = 2; // normal
        character.Paperdoll.Ring1 = 3; // cursed
        var repository = CreateRepository(
            (1, ItemSpecial.Cursed),
            (2, ItemSpecial.Normal),
            (3, ItemSpecial.Cursed));

        // Act
        var result = character.RemoveCursedEquipment(repository);

        // Assert
        result.Should().BeTrue();
        character.Paperdoll.Hat.Should().Be(0);
        character.Paperdoll.Ring1.Should().Be(0);
        character.Paperdoll.Armor.Should().Be(2, "non-cursed equipment must stay equipped");
    }

    [Fact]
    public void RemoveCursedEquipment_WhenNothingCursed_ShouldReturnFalseAndLeaveEquipment()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Paperdoll.Hat = 1;
        character.Paperdoll.Armor = 2;
        var repository = CreateRepository(
            (1, ItemSpecial.Normal),
            (2, ItemSpecial.Rare));

        // Act
        var result = character.RemoveCursedEquipment(repository);

        // Assert
        result.Should().BeFalse();
        character.Paperdoll.Hat.Should().Be(1);
        character.Paperdoll.Armor.Should().Be(2);
    }

    [Fact]
    public void RemoveCursedEquipment_ShouldDestroyItemsRatherThanReturnThemToInventory()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Paperdoll.Weapon = 1; // cursed
        var repository = CreateRepository((1, ItemSpecial.Cursed));

        // Act
        var result = character.RemoveCursedEquipment(repository);

        // Assert
        result.Should().BeTrue();
        character.Paperdoll.Weapon.Should().Be(0);
        character.Inventory.Items.Should().BeEmpty();
    }
}
