# Prompt: Create New Game Service

Use this template when creating a new game service for the Acorn server.

## Instructions

Create a new game service that handles `{ServiceDescription}`.

### Requirements

1. **Interface**: `src/Acorn/Game/Services/I{ServiceName}Service.cs`
2. **Implementation**: `src/Acorn/Game/Services/{ServiceName}Service.cs`
3. **Tests**: `tests/Acorn.Tests/Game/Services/{ServiceName}ServiceTests.cs`
4. **Registration**: Add to DI in `Program.cs`

### Checklist

- [ ] Create interface with XML documentation
- [ ] Create implementation
- [ ] Register in DI container
- [ ] Write unit tests
- [ ] Handle edge cases (null, negative amounts, etc.)

### Interface Template

```csharp
namespace Acorn.Game.Services;

/// <summary>
/// Manages {description} operations.
/// </summary>
public interface I{ServiceName}Service
{
    /// <summary>
    /// Attempts to {action description}.
    /// </summary>
    /// <param name="character">The character to modify.</param>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="amount">The amount (default 1).</param>
    /// <returns>True if successful; otherwise false.</returns>
    bool TryAddItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Attempts to remove an item.
    /// </summary>
    bool TryRemoveItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Checks if the character has the specified item.
    /// </summary>
    bool HasItem(Character character, int itemId, int amount = 1);

    /// <summary>
    /// Gets the amount of a specific item.
    /// </summary>
    int GetItemAmount(Character character, int itemId);
}
```

### Implementation Template

```csharp
using Acorn.Domain.Models;

namespace Acorn.Game.Services;

/// <summary>
/// Default implementation of {description} management.
/// </summary>
public class {ServiceName}Service : I{ServiceName}Service
{
    public bool TryAddItem(Character character, int itemId, int amount = 1)
    {
        // Guard clause
        if (amount <= 0)
        {
            return false;
        }

        // Try to stack with existing item
        var existingItem = character.{Collection}.Items.FirstOrDefault(i => i.Id == itemId);
        if (existingItem != null)
        {
            existingItem.Amount += amount;
            return true;
        }

        // Add new item
        character.{Collection}.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return true;
    }

    public bool TryRemoveItem(Character character, int itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return false;
        }

        var item = character.{Collection}.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Amount < amount)
        {
            return false;
        }

        item.Amount -= amount;

        // Remove empty stacks
        if (item.Amount <= 0)
        {
            // Rebuild collection without empty item
            var newItems = new ConcurrentBag<ItemWithAmount>(
                character.{Collection}.Items.Where(i => i.Id != itemId || i.Amount > 0)
            );
            character.{Collection} = new {Collection}(newItems);
        }

        return true;
    }

    public bool HasItem(Character character, int itemId, int amount = 1)
    {
        var item = character.{Collection}.Items.FirstOrDefault(i => i.Id == itemId);
        return item != null && item.Amount >= amount;
    }

    public int GetItemAmount(Character character, int itemId)
    {
        return character.{Collection}.Items.FirstOrDefault(i => i.Id == itemId)?.Amount ?? 0;
    }
}
```

### Test Template

```csharp
using Acorn.Database.Models;
using Acorn.Domain.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class {ServiceName}ServiceTests
{
    private readonly {ServiceName}Service _sut;

    public {ServiceName}ServiceTests()
    {
        _sut = new {ServiceName}Service();
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

    [Fact]
    public void TryAddItem_WhenValidAmount_ShouldAddNewItem()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.{Collection}.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Id = 1, Amount = 10 });
    }

    [Fact]
    public void TryAddItem_WhenItemExists_ShouldStackAmount()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.{Collection}.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeTrue();
        character.{Collection}.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void TryAddItem_WhenInvalidAmount_ShouldReturnFalse(int invalidAmount)
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.TryAddItem(character, itemId: 1, amount: invalidAmount);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryRemoveItem_WhenSufficientAmount_ShouldRemove()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.{Collection}.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 5);

        // Assert
        result.Should().BeTrue();
        character.{Collection}.Items.Should().ContainSingle()
            .Which.Amount.Should().Be(5);
    }

    [Fact]
    public void TryRemoveItem_WhenInsufficientAmount_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.{Collection}.Items.Add(new ItemWithAmount { Id = 1, Amount = 5 });

        // Act
        var result = _sut.TryRemoveItem(character, itemId: 1, amount: 10);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasItem_WhenItemExists_ShouldReturnTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.{Collection}.Items.Add(new ItemWithAmount { Id = 1, Amount = 10 });

        // Act
        var result = _sut.HasItem(character, itemId: 1, amount: 5);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasItem_WhenItemDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.HasItem(character, itemId: 999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetItemAmount_WhenItemExists_ShouldReturnAmount()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.{Collection}.Items.Add(new ItemWithAmount { Id = 1, Amount = 42 });

        // Act
        var result = _sut.GetItemAmount(character, itemId: 1);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
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
```

### DI Registration

Add to `Program.cs`:

```csharp
services
    .AddSingleton<I{ServiceName}Service, {ServiceName}Service>()
```

Or in an extension method in `src/Acorn/Extensions/IocExtensions.cs`.

### Service Lifetime Guidelines

| Lifetime | When to Use |
|----------|------------|
| `Singleton` | Stateless services, no scoped dependencies |
| `Scoped` | Needs DbContext or per-request state |
| `Transient` | Lightweight, no shared state |

Most game services should be **Singleton** since they're stateless and operate on passed-in Character objects.
