using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Game.Services;

public class InventoryServiceTests
{
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _sut = new InventoryService();
    }

    private static Character CreateTestCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Inventory = new Inventory([]),
            Bank = new Bank([]),
            Paperdoll = new Paperdoll(),
            Spells = new Spells([])
        };
    }

    [Test]
    public void TryAddItem_WhenValidAmount_ShouldAddNewItem()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Inventory.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Id = 1, Amount = 10 });
    }

    [Test]
    public void TryAddItem_WhenItemExists_ShouldStackAmount()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Inventory.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(15);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(-100)]
    public void TryAddItem_WhenInvalidAmount_ShouldReturnFalse(int invalidAmount)
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: invalidAmount);

        // Assert
        result.Should().BeFalse();
        character.Inventory.Items.Should().BeEmpty();
    }

    [Test]
    public void TryRemoveItem_WhenItemExistsWithSufficientAmount_ShouldRemove()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 5);

        // Assert
        result.Should().BeTrue();
        character.Inventory.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(5);
    }

    [Test]
    public void TryRemoveItem_WhenRemovingExactAmount_ShouldRemoveItemCompletely()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Inventory.Items.Should().BeEmpty();
    }

    [Test]
    public void TryRemoveItem_WhenInsufficientAmount_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeFalse();
        character.Inventory.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(5);
    }

    [Test]
    public void TryRemoveItem_WhenItemDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 999, amount: 1);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void TryRemoveItem_WhenInvalidAmount_ShouldReturnFalse(int invalidAmount)
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: invalidAmount);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void HasItem_WhenItemExistsWithSufficientAmount_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.HasItem(character, itemId: 1, amount: 5);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void HasItem_WhenItemExistsWithExactAmount_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.HasItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void HasItem_WhenInsufficientAmount_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.HasItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void HasItem_WhenItemDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.HasItem(character, itemId: 999);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void GetItemAmount_WhenItemExists_ShouldReturnAmount()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 42 });

        // Act
        var result = _sut.GetItemAmount(character, itemId: 1);

        // Assert
        result.Should().Be(42);
    }

    [Test]
    public void GetItemAmount_WhenItemDoesNotExist_ShouldReturnZero()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.GetItemAmount(character, itemId: 999);

        // Assert
        result.Should().Be(0);
    }

    [Test]
    public void GetSlotCount_ShouldReturnNumberOfItemSlots()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });
        character.Inventory.Items.Add(new ItemWithAmount { Id = 2, Amount = 5 });
        character.Inventory.Items.Add(new ItemWithAmount { Id = 3, Amount = 1 });

        // Act
        var result = _sut.GetSlotCount(character);

        // Assert
        result.Should().Be(3);
    }

    [Test]
    public void GetSlotCount_WhenEmpty_ShouldReturnZero()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.GetSlotCount(character);

        // Assert
        result.Should().Be(0);
    }

    // --- Weight enforcement ---

    private static Eif CreateEif(params (int Id, int Weight)[] items)
    {
        var weights = items.ToDictionary(i => i.Id, i => i.Weight);
        var maxId = weights.Count == 0 ? 0 : weights.Keys.Max();

        var eifItems = new List<EifRecord>();
        for (var id = 1; id <= maxId; id++)
        {
            eifItems.Add(new EifRecord
            {
                Name = $"Item{id}",
                Weight = weights.TryGetValue(id, out var weight) ? weight : 0
            });
        }

        return new Eif { Items = eifItems };
    }

    private static IDataFileRepository CreateRepository(Eif eif)
    {
        var repository = Substitute.For<IDataFileRepository>();
        repository.Eif.Returns(eif);
        return repository;
    }

    [Test]
    public void CanHoldItem_WhenWithinWeightLimit_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 100;
        var eif = CreateEif((1, 10));

        // Act
        var result = _sut.CanHoldItem(character, eif, itemId: 1, amount: 2);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void CanHoldItem_WhenExceedsWeightLimit_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 15;
        var eif = CreateEif((1, 10));

        // Act
        var result = _sut.CanHoldItem(character, eif, itemId: 1, amount: 2);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void CanHoldItem_WhenExistingInventoryCountsTowardWeight_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 100;
        character.Inventory.Items.Add(new ItemWithAmount { Id = 1, Amount = 8 });
        var eif = CreateEif((1, 10));

        // Act
        var result = _sut.CanHoldItem(character, eif, itemId: 1, amount: 3);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void CanHoldItem_WhenItemHasNoWeight_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 0;
        var eif = CreateEif((1, 0));

        // Act
        var result = _sut.CanHoldItem(character, eif, itemId: 1, amount: 100);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void CanHoldItem_WhenItemNotInEif_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 0;
        var eif = CreateEif();

        // Act
        var result = _sut.CanHoldItem(character, eif, itemId: 999, amount: 1);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void TryAddItem_WithRepository_WhenOverweight_ShouldReturnFalseAndNotAdd()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 5;
        var eif = CreateEif((1, 10));
        var sut = new InventoryService(new WeightCalculator(), CreateRepository(eif));

        // Act
        var result = sut.TryAddItem(character, itemId: 1, amount: 1);

        // Assert
        result.Should().BeFalse();
        character.Inventory.Items.Should().BeEmpty();
    }

    [Test]
    public void TryAddItem_WithRepository_WhenWithinWeight_ShouldAdd()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.MaxWeight = 100;
        var eif = CreateEif((1, 10));
        var sut = new InventoryService(new WeightCalculator(), CreateRepository(eif));

        // Act
        var result = sut.TryAddItem(character, itemId: 1, amount: 1);

        // Assert
        result.Should().BeTrue();
        character.Inventory.Items.Should().ContainSingle(i => i.Id == 1 && i.Amount == 1);
    }
}