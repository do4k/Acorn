# Acorn Testing Guide

## Testing Stack

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| NSubstitute | Mocking library |
| FluentAssertions | Assertion library |
| Coverlet | Code coverage |

## Test Project Structure

```
tests/
└── Acorn.Tests/
    ├── Acorn.Tests.csproj
    └── Game/
        ├── Models/
        │   └── CharacterTests.cs
        └── Services/
            ├── BankServiceTests.cs
            └── InventoryServiceTests.cs
```

## Test Naming Convention

```csharp
[Fact]
public void MethodName_WhenCondition_ShouldExpectedBehavior()
```

Examples:
- `TryAddItem_WhenValidAmount_ShouldAddNewItem`
- `TryAddItem_WhenItemExists_ShouldStackAmount`
- `TryRemoveItem_WhenInsufficientAmount_ShouldReturnFalse`
- `HasItem_WhenItemDoesNotExist_ShouldReturnFalse`

## Test Structure (AAA Pattern)

```csharp
[Fact]
public void TryAddItem_WhenValidAmount_ShouldAddNewItem()
{
    // Arrange - Set up test data and dependencies
    var character = CreateTestCharacter();
    var sut = new InventoryService();

    // Act - Execute the method under test
    var result = sut.TryAddItem(character, itemId: 1, amount: 10);

    // Assert - Verify the results
    result.Should().BeTrue();
    character.Inventory.Items.Should().ContainSingle()
        .Which.Should().BeEquivalentTo(new { Id = 1, Amount = 10 });
}
```

## FluentAssertions Patterns

### Basic Assertions

```csharp
// Boolean
result.Should().BeTrue();
result.Should().BeFalse();

// Null checks
character.Should().NotBeNull();
result.Should().BeNull();

// Numeric
amount.Should().Be(42);
count.Should().BeGreaterThan(0);
value.Should().BeInRange(1, 100);
```

### Collection Assertions

```csharp
// Single item
character.Inventory.Items.Should().ContainSingle();
character.Inventory.Items.Should().ContainSingle()
    .Which.Amount.Should().Be(15);

// Empty/not empty
items.Should().BeEmpty();
items.Should().NotBeEmpty();
items.Should().HaveCount(3);

// Contains
items.Should().Contain(x => x.Id == 1);
items.Should().NotContain(x => x.Id == 999);

// Object equivalence
item.Should().BeEquivalentTo(new { Id = 1, Amount = 10 });
```

### Exception Assertions

```csharp
// Should throw
Action act = () => service.DoSomething(invalidInput);
act.Should().Throw<ArgumentException>();

// Should throw with message
act.Should().Throw<ArgumentException>()
    .WithMessage("*invalid*");

// Should not throw
Action act = () => service.DoSomething(validInput);
act.Should().NotThrow();
```

## Creating Test Data

### Helper Methods

```csharp
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

    private static Character CreateCharacterWithItem(int itemId, int amount)
    {
        var character = CreateTestCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
        return character;
    }
}
```

## Theory Tests (Parameterized)

```csharp
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
    character.Inventory.Items.Should().BeEmpty();
}

[Theory]
[InlineData(1, 10, 5, 15)]   // Add 5 to 10 = 15
[InlineData(1, 100, 50, 150)] // Add 50 to 100 = 150
public void TryAddItem_WhenStacking_ShouldCalculateCorrectly(
    int itemId, int initial, int addAmount, int expectedTotal)
{
    // Arrange
    var character = CreateCharacterWithItem(itemId, initial);

    // Act
    _sut.TryAddItem(character, itemId, addAmount);

    // Assert
    character.Inventory.Items.First(i => i.Id == itemId).Amount.Should().Be(expectedTotal);
}
```

## Mocking with NSubstitute

### Basic Mocking

```csharp
public class SomeHandlerTests
{
    private readonly ILogger<SomeHandler> _logger;
    private readonly ISomeService _someService;
    private readonly SomeHandler _sut;

    public SomeHandlerTests()
    {
        _logger = Substitute.For<ILogger<SomeHandler>>();
        _someService = Substitute.For<ISomeService>();
        _sut = new SomeHandler(_logger, _someService);
    }

    [Fact]
    public async Task HandleAsync_WhenValidRequest_ShouldCallService()
    {
        // Arrange
        var player = CreateTestPlayerState();
        var packet = new SomePacket { ItemId = 1 };

        _someService.TryDoSomething(Arg.Any<Character>(), Arg.Any<int>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(player, packet);

        // Assert
        _someService.Received(1).TryDoSomething(
            Arg.Is<Character>(c => c.Name == "TestCharacter"),
            Arg.Is<int>(id => id == 1));
    }
}
```

### Async Mocking

```csharp
// Setup async return
_repository.GetByIdAsync(Arg.Any<int>())
    .Returns(Task.FromResult<Character?>(testCharacter));

// Verify async call
await _repository.Received(1).SaveAsync(Arg.Any<Character>());
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~InventoryServiceTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~TryAddItem_WhenValidAmount_ShouldAddNewItem"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## What to Test

### Services (High Priority)

- All public methods
- Edge cases (empty collections, zero amounts, null handling)
- Business logic validation
- State mutations

### Packet Handlers (Medium Priority)

- Valid request handling
- Invalid state handling (null character, null map)
- Invalid packet data
- Response packet generation

### Models (Lower Priority)

- Computed properties
- Validation logic
- Edge cases in calculations

## Test File Template

```csharp
using Acorn.Domain.Models;
using Acorn.Game.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class MyServiceTests
{
    private readonly MyService _sut;

    public MyServiceTests()
    {
        _sut = new MyService();
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
    public void Method_WhenCondition_ShouldExpectedBehavior()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _sut.Method(character);

        // Assert
        result.Should().BeTrue();
    }
}
```
