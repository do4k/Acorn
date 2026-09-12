# Agents Guide for Project Acorn

> A modern C# server emulator for Endless Online, built with .NET 10 and Entity Framework Core

## Project Overview

**Acorn** is a game server emulator for the 2D MMORPG "Endless Online". It handles player connections via TCP/WebSocket, manages game world state, processes client packets, and persists data to multiple database backends.

### Tech Stack

| Component | Technology |
|-----------|------------|
| Language | C# 13+ |
| Framework | .NET 10 |
| ORM | Entity Framework Core 10 |
| Databases | SQLite (dev), MySQL, PostgreSQL, SQL Server |
| Caching | Redis / In-Memory |
| Testing | TUnit, NSubstitute, FluentAssertions |
| Protocol | Moffat.EndlessOnline.SDK (eolib-dotnet) |
| API | ASP.NET Core Minimal APIs |

## Repository Structure

```
acorn/
├── src/
│   ├── Acorn/                  # Main game server (console app)
│   │   ├── Data/               # Game data files (drops, quests, news)
│   │   ├── Database/           # Data loaders and scripts
│   │   ├── Extensions/         # DI and helper extensions
│   │   ├── Game/               # Game logic (services, mappers, models)
│   │   ├── Infrastructure/     # Networking, security, Gemini AI
│   │   ├── Net/                # Packet handlers and player state
│   │   ├── Options/            # Configuration classes
│   │   ├── SLN/                # Server Link Network integration
│   │   └── World/              # World state, maps, NPCs, services
│   ├── Acorn.Api/              # REST API for game state queries
│   ├── Acorn.Database/         # EF Core DbContext, entities and repositories
│   └── Acorn.Shared/           # Shared contract models, caching, extensions, options
├── tests/
│   └── Acorn.Tests/            # Unit tests
├── docs/                       # Documentation
└── .ai/                        # AI agent context and prompts
```

## Quick Reference

### Build & Run

```bash
# Build
dotnet build

# Apply database migrations (SQLite default) - required before running the server
cd src/Acorn && dotnet ef database update --project ../Acorn.Database --startup-project .

# Run (SQLite default)
dotnet run --project src/Acorn

# Run the full stack; applies migrations automatically
scripts/run-apphost.sh

# Run with specific database
dotnet run --project src/Acorn --environment=PostgreSQL

# Test
dotnet test

# Build for release
dotnet build -c Release
```

### Key Entry Points

| File | Purpose |
|------|---------|
| `src/Acorn/Program.cs` | Main server entry, DI configuration |
| `src/Acorn.Api/Program.cs` | REST API entry |
| `src/Acorn.Database/AcornDbContext.cs` | Database context |

## Architecture Patterns

### Packet Handlers

Network packets are handled by classes implementing `IPacketHandler<TPacket>`. Located in `src/Acorn/Net/PacketHandlers/`.

```csharp
public interface IPacketHandler<in TPacket> : IPacketHandler where TPacket : IPacket
{
    Task HandleAsync(PlayerState playerState, TPacket packet);
}
```

Handlers are organized by category (Account, Bank, Character, Item, Player, etc.) and auto-registered via `AddPacketHandlers()`.

### Services

Game services follow interface-based design for testability:

- `IInventoryService` - Inventory management
- `IBankService` - Banking operations  
- `IPaperdollService` - Equipment management
- `IMapController` - Map state management
- `INpcController` - NPC behavior and combat
- `IPlayerController` - Player actions and state

### World State

The game world is managed through:
- `WorldState` - Global world container
- `MapState` - Per-map state (players, NPCs, items)
- `NpcState` - Individual NPC state and behavior
- `PlayerState` - Connected player session state

### Caching

Two-tier caching via `ICacheService`:
- Redis (production) - Distributed cache
- In-Memory (development) - Simple fallback

## Code Conventions

### Naming

- **Interfaces**: Prefix with `I` (e.g., `IInventoryService`)
- **Classes**: PascalCase
- **Methods**: PascalCase
- **Private fields**: camelCase or _prefixed
- **File-scoped namespaces**: Preferred

### File Organization

- **One top-level type per file.** Each `class`, `interface`, `struct`, `enum`, `record`, and delegate gets its own `.cs` file, named after the type (`IChatSanitizer` → `IChatSanitizer.cs`).
- Do not co-locate an interface, its implementation, and helpers in a single file. Private helper types get their own file too.
- Nested types may live inside their containing type.
- File-scoped namespaces (already preferred) reinforce this.

```csharp
// Bad: one ChatSanitizer.cs containing IChatSanitizer, ChatSanitizer and ChatText
// Good:
//   IChatSanitizer.cs  -> public interface IChatSanitizer { ... }
//   ChatSanitizer.cs   -> public class ChatSanitizer : IChatSanitizer { ... }
//   ChatText.cs        -> internal static class ChatText { ... }
```

Enforcement is configured in `.editorconfig` via the StyleCop rules `SA1402` (file may only contain a single type) and `SA1649` (file name must match the first type). Both are set to `warning`, so with `TreatWarningsAsErrors` the build fails on violations. All other StyleCop categories are disabled so only these two rules are active.

### Testing Patterns

Tests use the Arrange-Act-Assert pattern with FluentAssertions:

```csharp
[Test]
public void MethodName_WhenCondition_ShouldExpectedBehavior()
{
    // Arrange
    var sut = new ServiceUnderTest();
    
    // Act
    var result = sut.Method();
    
    // Assert
    result.Should().BeTrue();
}
```

### Project References

- `Acorn` depends on `Acorn.Shared`, `Acorn.Database`
- `Acorn.Api` depends on `Acorn.Shared`, `Acorn.Database`
- `Acorn.Database` depends on `Acorn.Shared`
- `Acorn.Shared` is standalone

## Common Tasks

### Adding a New Packet Handler

1. Create handler in `src/Acorn/Net/PacketHandlers/{Category}/`
2. Implement `IPacketHandler<TPacket>`
3. Handler is auto-registered by DI

### Adding a New Service

1. Create interface in appropriate location
2. Create implementation
3. Register in `Program.cs` or extension method
4. Add unit tests in `tests/Acorn.Tests/`

### Adding a Database Migration

```bash
# dotnet-ef is a local tool; restore it once from the repo root
dotnet tool restore

# Add a migration after changing the model
dotnet ef migrations add MigrationName --project src/Acorn.Database --startup-project src/Acorn

# Apply it to the development database
cd src/Acorn && dotnet ef database update --project ../Acorn.Database --startup-project .
```

Migrations live in `src/Acorn.Database/Migrations` and are applied as a startup step by
`scripts/run-apphost.sh`; the server does not create or upgrade schemas at runtime.

### Adding a New API Endpoint

1. Create feature file in `src/Acorn.Api/Features/`
2. Use minimal API pattern with `app.MapGet/MapPost`
3. Register in `Program.cs`

## Important Files

| File | What it does |
|------|--------------|
| `global.json` | .NET SDK version (10.0.0) |
| `.editorconfig` | Code style rules |
| `appsettings.json` | Server configuration |
| `docker-compose.yml` | Multi-database Docker setup |
| `.github/workflows/ci.yml` | CI pipeline |

## AI Agent Guidelines

### Do

- Follow existing code patterns and naming conventions
- Put each interface, enum, class, struct, and record in its own file
- Write unit tests for new services
- Use dependency injection
- Prefer async/await for I/O operations
- Add XML documentation for public APIs
- Use `ILogger<T>` for logging

### Don't

- Commit secrets or connection strings
- Skip the interface when creating services
- Modify database schema without migrations
- Break existing packet handler contracts

### When Modifying

- **Packet handlers**: Check the EO protocol SDK docs
- **Database models**: Create migrations, update DbContext
- **Services**: Add corresponding tests
- **World state**: Consider thread safety

## Documentation

- [Database Configuration](docs/DATABASE.md)
- [Caching Layer](docs/CACHING.md)
- [REST API](docs/API.md)
- [Gemini AI Integration](docs/GEMINI_WISEMAN.md)
- [Inventory System](docs/INVENTORY.md)
- [Redis Real-time State](docs/REDIS_REALTIME.md)

## Additional Context

See the `.ai/` directory for:
- `context/` - Detailed architecture and convention docs
- `prompts/` - Templates for common development tasks
