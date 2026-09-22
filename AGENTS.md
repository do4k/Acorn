# Agents Guide for Project Acorn

> A modern C# server emulator for Endless Online, built with .NET 11 and Entity Framework Core

## Project Overview

**Acorn** is a game server emulator for the 2D MMORPG "Endless Online". It handles player connections via TCP/WebSocket, manages game world state, processes client packets, and persists data to multiple database backends.

### Tech Stack

| Component | Technology |
|-----------|------------|
| Language | C# 15+ |
| Framework | .NET 11 |
| ORM | Entity Framework Core 11 |
| Databases | SQLite (dev), MySQL, PostgreSQL, SQL Server |
| Caching | In-Memory (Redis optional via AppHost) |
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
│   ├── Acorn.AppHost/          # .NET Aspire orchestration for the full stack
│   ├── Acorn.Database/         # EF Core DbContext, entities and repositories
│   ├── Acorn.Database.PostgreSql/  # PostgreSQL-specific EF Core extensions
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

## Deployment

### Current setup (manual, agent-orchestrated)

The hosted stack runs with Docker Compose on this host using the **postgres**
profile. There is no CI runner on the host yet: deployments are orchestrated by an
AI agent running on the host. When asked to "deploy the latest", the agent fetches
`origin/main`, rebuilds the images, applies migrations, recreates the app
containers and verifies health.

Containers and the images they run:

| Container | Image | Notes |
|-----------|-------|-------|
| `acorn-postgres` | `acorn-acorn-postgres` | Game server: TCP `8078`, WebSocket `8079` (localhost) |
| `acorn-api-postgres` | `acorn-acorn-api-postgres` | REST API: `5000` → `8080` |
| `acorn-postgres-db` | `postgres:17-alpine` | Database, published on `5432`; data in the `acorn_postgres-data` volume |
| `acorn-caddy` | `caddy:2` | TLS reverse proxy, landing page and eoweb client (`80`/`443`) |
| `acorn-aspire` | `aspire-dashboard` | OTel UI on `127.0.0.1:18888` |

Compose settings come from the repo-root `.env` (`COMPOSE_PROFILES=postgres,true`,
`BASE_DOMAIN`, `CACHE_ENABLED`, ...). Recreating containers preserves the database
volume, so data survives a redeploy.

### Agent deployment checklist

1. **Get the latest `main`.** `main` may be checked out in another worktree
   (`../acorn.worktrees/main-deploy`); in that case detach it first, or use
   `git checkout --detach origin/main`. Preserve any uncommitted work with
   `git stash push --include-untracked` before switching.
2. **Fetch the game data** (map `*.emf`, `Data/*.edf`, pub, ...). These files
   are copyrighted and deliberately **not in git** - a clean worktree only has
   the tracked `*.cs` files under `src/Acorn/Data`, and both the image build
   and the runtime bind mount need the full set (a mapless server disconnects
   every character on login, issue #144):
   ```bash
   scripts/fetch-game-data.sh   # downloads from https://<BASE_DOMAIN>/gamedata/
   ```
   The static copy is refreshed from a checkout that has the data with
   `scripts/publish-game-data.sh` (served by Caddy from
   `$GAME_DATA_PUBLISH_DIR`, default `/srv/acorn/game-data`).
3. **Apply migrations** (see below) - always before restarting the server.
4. **Rebuild the images** from the repo root:
   ```bash
   docker build -f src/Acorn/Dockerfile -t acorn-acorn-postgres:latest .
   docker build -f src/Acorn.Api/Dockerfile -t acorn-acorn-api-postgres:latest .
   ```
   The compose service image names are `<project>-<service>`, i.e.
   `acorn-acorn-postgres` / `acorn-acorn-api-postgres` for the `acorn` project.
5. **Recreate only the app containers**, reusing the DB/network/`.env`:
   ```bash
   docker compose --profile postgres up -d --no-build --force-recreate \
     acorn-postgres acorn-api-postgres
   ```
6. **Verify:** `docker ps` shows both healthy, `docker logs --tail 50
   acorn-postgres` shows the world/listeners started **and `Loaded <n> maps`
   with n > 0**.

### Database migrations

The server never creates or upgrades the schema at runtime, and `docker compose up`
does **not** apply migrations - run them explicitly. Migrations are provider
specific:

- SQLite (local dev): `src/Acorn.Database/Migrations`
- PostgreSQL (deployed): `src/Acorn.Database.PostgreSql/Migrations`

Apply the PostgreSQL migrations against the database published on `localhost:5432`.
`ASPNETCORE_ENVIRONMENT=Production` is required so design-time scope validation does
not stop the host from starting:

```bash
dotnet tool restore
cd src/Acorn
ASPNETCORE_ENVIRONMENT=Production Database__Engine=PostgreSQL \
  Database__ConnectionString="Host=localhost;Port=5432;Database=acorn;Username=acorn;Password=acornpassword" \
  dotnet ef database update --project ../Acorn.Database.PostgreSql --startup-project .
```

A successful no-op run prints `No migrations were applied. The database is already
up to date.` Confirm the applied rows match the migration files:

```bash
docker exec acorn-postgres-db psql -U acorn -d acorn \
  -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
```

### Future: automated deploys

The goal is a self-hosted GitHub Actions runner on this host that, on every push to
`main` (i.e. after a PR merge), pulls the latest revision, applies migrations and
runs the same build-and-recreate flow above. Until that exists, redeploys stay
manual and agent-orchestrated.

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

Caching is in-memory via `ICacheService` (`InMemoryCacheService`), plus specialised
caches for pub files, realtime map state and online characters:

- `ICacheService` - Generic key/value cache (in-memory)
- `IPubCacheService` - Cached pub data (items, NPCs, spells, classes)
- `IMapCacheService` - Realtime map state
- `ICharacterCacheService` - Online character state

`AddCaching()` registers these; caching can be disabled with `Cache:Enabled`.
Redis is not wired up - `Acorn.AppHost` keeps an optional, commented-out Redis
resource for local experimentation.

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

- `Acorn` depends on `Acorn.Shared`, `Acorn.Database`, `Acorn.Database.PostgreSql`
- `Acorn.Api` depends on `Acorn.Shared`, `Acorn.Database`, `Acorn.Database.PostgreSql`
- `Acorn.AppHost` depends on `Acorn`, `Acorn.Api`
- `Acorn.Database.PostgreSql` depends on `Acorn.Database`
- `Acorn.Database` depends on `Acorn.Shared`
- `Acorn.Shared` is standalone
- `Acorn.Tests` depends on `Acorn`

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
PostgreSQL uses its own migrations assembly (`src/Acorn.Database.PostgreSql/Migrations`),
which is what the hosted deployment needs - see [Deployment](#deployment) for the exact
apply command and how to confirm the database is up to date.

### Adding a New API Endpoint

1. Create feature file in `src/Acorn.Api/Features/`
2. Use minimal API pattern with `app.MapGet/MapPost`
3. Register in `Program.cs`

## Important Files

| File | What it does |
|------|--------------|
| `global.json` | .NET SDK version (11.0.100-rc.1.26425.128) |
| `.editorconfig` | Code style rules |
| `src/Acorn/appsettings.json` | Server configuration |
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
- [Command Reference](docs/COMMANDS.md)
- [Gemini AI Integration](docs/GEMINI_WISEMAN.md)
- [Inventory System](docs/INVENTORY.md)
- [Shops](docs/SHOPS.md)
- [Codebase Review](docs/CODEBASE_REVIEW.md)

## Additional Context

See the `.ai/` directory for:
- `context/` - Detailed architecture and convention docs
- `prompts/` - Templates for common development tasks
