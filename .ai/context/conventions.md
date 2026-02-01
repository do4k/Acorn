# Acorn Coding Conventions

## C# Style Guide

### File Structure

```csharp
// 1. Usings (sorted, no unused)
using System.Collections.Concurrent;
using Acorn.Domain.Models;

// 2. File-scoped namespace (preferred)
namespace Acorn.Game.Services;

// 3. XML documentation for public types
/// <summary>
/// Manages player inventory operations.
/// </summary>
public class InventoryService : IInventoryService
{
    // 4. Private fields first
    private readonly ILogger<InventoryService> _logger;

    // 5. Constructor
    public InventoryService(ILogger<InventoryService> logger)
    {
        _logger = logger;
    }

    // 6. Public methods
    public bool TryAddItem(Character character, int itemId, int amount = 1)
    {
        // Implementation
    }
}
```

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Interfaces | `I` prefix, PascalCase | `IInventoryService` |
| Classes | PascalCase | `InventoryService` |
| Methods | PascalCase | `TryAddItem` |
| Properties | PascalCase | `Character.Name` |
| Private fields | `_camelCase` | `_logger` |
| Parameters | camelCase | `itemId` |
| Local variables | camelCase | `existingItem` |
| Constants | PascalCase | `MaxInventorySlots` |

### Method Naming Patterns

```csharp
// Try pattern - returns bool, out parameter or modifies state
bool TryAddItem(Character character, int itemId, int amount);
bool TryRemoveItem(Character character, int itemId, int amount);

// Has/Is pattern - boolean checks
bool HasItem(Character character, int itemId);
bool IsEquipped(Character character, int itemId);

// Get pattern - retrieves data
int GetItemAmount(Character character, int itemId);
Character? GetCharacterByName(string name);

// Async suffix for async methods
Task<Account?> GetByUsernameAsync(string username);
Task HandleAsync(PlayerState playerState, TPacket packet);
```

### Guard Clauses

```csharp
public bool TryAddItem(Character character, int itemId, int amount = 1)
{
    // Guard clauses at the start
    if (amount <= 0)
    {
        return false;
    }

    if (character.Inventory.Items.Count >= MaxSlots)
    {
        return false;
    }

    // Main logic follows
    // ...
}
```

## Dependency Injection

### Service Registration

```csharp
// In Program.cs or extension methods
services
    .AddSingleton<IInventoryService, InventoryService>()
    .AddScoped<ICharacterRepository, CharacterRepository>()
    .AddTransient<ISomeFactory, SomeFactory>();
```

### Constructor Injection

```csharp
// Use primary constructors for simple cases
public class BankOpenClientPacketHandler(ILogger<BankOpenClientPacketHandler> logger)
    : IPacketHandler<BankOpenClientPacket>
{
    // logger is available as a field
}

// Traditional constructors for complex initialization
public class WorldState
{
    private readonly ILogger<WorldState> _logger;

    public WorldState(
        IDataFileRepository dataRepository,
        MapStateFactory mapStateFactory,
        ILogger<WorldState> logger)
    {
        _logger = logger;
        // Complex initialization...
    }
}
```

## Async/Await

### Guidelines

```csharp
// Always use async/await for I/O
public async Task<Character?> GetByNameAsync(string name)
{
    return await _context.Characters
        .FirstOrDefaultAsync(c => c.Name == name);
}

// Use ConfigureAwait(false) in library code (optional in ASP.NET Core)
public async Task<T?> GetAsync<T>(string key)
{
    var value = await _cache.GetAsync(key).ConfigureAwait(false);
    // ...
}

// Avoid async void except for event handlers
public async Task HandleAsync(...) // Good
public async void HandleAsync(...) // Bad - exceptions are lost
```

## Logging

### Usage

```csharp
public class SomeService
{
    private readonly ILogger<SomeService> _logger;

    public void DoSomething(int itemId)
    {
        // Use structured logging with templates
        _logger.LogInformation("Processing item {ItemId}", itemId);

        // Log levels:
        _logger.LogDebug("Detailed info for debugging");
        _logger.LogInformation("Normal operational messages");
        _logger.LogWarning("Unexpected but handled situations");
        _logger.LogError(exception, "Something failed for {ItemId}", itemId);
    }
}
```

## Error Handling

### Patterns

```csharp
// Return false/null for expected failures
public bool TryAddItem(...) => amount <= 0 ? false : ...;
public Character? GetCharacter(...) => ...;

// Throw for programmer errors
public void SetItem(int itemId, int amount)
{
    ArgumentOutOfRangeException.ThrowIfNegative(amount);
    // ...
}

// Use try-catch for external failures
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Failed to save character");
    throw;
}
```

## Packet Handlers

### Structure

```csharp
public class SomeActionClientPacketHandler(
    ILogger<SomeActionClientPacketHandler> logger,
    ISomeService someService)
    : IPacketHandler<SomeActionClientPacket>
{
    public async Task HandleAsync(PlayerState player, SomeActionClientPacket packet)
    {
        // 1. Validate player state
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Invalid state for {SessionId}", player.SessionId);
            return;
        }

        // 2. Validate packet data
        if (packet.Amount <= 0)
        {
            return;
        }

        // 3. Perform game logic
        var success = someService.TryDoSomething(player.Character, packet.ItemId);

        // 4. Send response
        if (success)
        {
            await player.Send(new SomeActionServerPacket { ... });
        }
    }

    // Required interface implementation
    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (SomeActionClientPacket)packet);
    }
}
```

## Thread Safety

### Concurrent Collections

```csharp
// Use concurrent collections for shared state
public ConcurrentDictionary<int, MapState> Maps = [];
public ConcurrentDictionary<int, PlayerState> Players = [];
public ConcurrentBag<ItemWithAmount> Items = [];

// Atomic operations
Maps.TryAdd(mapId, mapState);
Maps.TryRemove(mapId, out var removed);
Maps.TryGetValue(mapId, out var map);
```

## EditorConfig Rules

Key rules enforced by `.editorconfig`:

- **IDE0051**: Unused private members (warning)
- **IDE0052**: Unread private members (warning)
- **CA1801**: Unused parameters (warning)
- **CS8019**: Unnecessary using directives (warning)
- **Indentation**: 4 spaces
- **Braces**: Preferred on separate lines
- **File-scoped namespaces**: Preferred
