using System.Collections.Concurrent;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;

namespace Acorn.Tests.Game.Services;

public class BankServiceTests
{
    private readonly BankService _sut;

    public BankServiceTests()
    {
        _sut = new BankService();
    }

    private static Character CreateTestCharacter(int bankMax = 100)
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            BankMax = bankMax,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
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
        character.Bank.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Id = 1, Amount = 10 });
    }

    [Test]
    public void TryAddItem_WhenItemExists_ShouldStackAmount()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Bank.Items.Should().ContainSingle()
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
        character.Bank.Items.Should().BeEmpty();
    }

    [Test]
    public void TryAddItem_WhenBankFullAndItemExists_ShouldStack()
    {
        // Arrange
        var character = CreateTestCharacter(bankMax: 1);
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Bank.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(15);
    }

    [Test]
    public void TryAddItem_WhenBankFullAndNewItem_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter(bankMax: 1);
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryAddItem(character, itemId: 2, amount: 10);

        // Assert
        result.Should().BeFalse();
        character.Bank.Items.Should().ContainSingle()
            .Which.Id.Should().Be(1);
    }

    [Test]
    public void TryRemoveItem_WhenItemExistsWithSufficientAmount_ShouldRemove()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 5);

        // Assert
        result.Should().BeTrue();
        character.Bank.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(5);
    }

    [Test]
    public void TryRemoveItem_WhenRemovingExactAmount_ShouldRemoveItemCompletely()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.Bank.Items.Should().BeEmpty();
    }

    [Test]
    public void TryRemoveItem_WhenInsufficientAmount_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeFalse();
        character.Bank.Items.Should().ContainSingle()
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
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

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
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

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
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

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
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

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
        character.Bank.Items.Add(new ItemWithAmount { Id = 1, Amount = 42 });

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
}