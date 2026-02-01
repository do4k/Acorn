# Acorn Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                          Clients                                 │
│              (Endless Online game clients)                       │
└─────────────────────┬───────────────────────────────────────────┘
                      │ TCP:8078 / WebSocket:8079
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Acorn Server                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                 Network Layer                             │   │
│  │  TcpCommunicator ◄──────────► WebSocketCommunicator      │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           │                                      │
│                           ▼                                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │               Packet Handlers                             │   │
│  │  Account │ Bank │ Character │ Item │ Player │ NPC │ ...  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           │                                      │
│                           ▼                                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                  Game Services                            │   │
│  │  Inventory │ Bank │ Paperdoll │ Loot │ Stats │ Combat    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           │                                      │
│                           ▼                                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                   World State                             │   │
│  │  WorldState ──► MapState[] ──► NpcState[] / PlayerState[]│   │
│  └──────────────────────────────────────────────────────────┘   │
│                           │                                      │
│            ┌──────────────┴──────────────┐                      │
│            ▼                              ▼                      │
│  ┌─────────────────┐           ┌─────────────────┐              │
│  │   AcornDbContext │           │   ICacheService │              │
│  │   (EF Core)      │           │   (Redis/Memory)│              │
│  └─────────────────┘           └─────────────────┘              │
└─────────────────────────────────────────────────────────────────┘
              │                              │
              ▼                              ▼
┌─────────────────────┐           ┌─────────────────┐
│     Database        │           │      Redis      │
│ SQLite/MySQL/PG/SS  │           │    (optional)   │
└─────────────────────┘           └─────────────────┘
```

## Project Dependencies

```
Acorn.Shared (standalone utilities)
    ▲
    │
Acorn.Domain (domain models)
    ▲
    │
Acorn.Database (EF Core, repositories)
    ▲
    │
Acorn (main server)          Acorn.Api (REST API)
```

## Key Components

### Network Layer (`src/Acorn/Infrastructure/Communicators/`)

- **TcpCommunicator**: Traditional TCP socket communication
- **WebSocketCommunicator**: WebSocket protocol support
- Both use the EO protocol SDK for packet serialization

### Packet Handlers (`src/Acorn/Net/PacketHandlers/`)

Handlers process incoming client packets. Each handler:
1. Implements `IPacketHandler<TPacket>`
2. Receives `PlayerState` and the typed packet
3. Validates state and performs game logic
4. Sends response packets via `PlayerState.Send()`

Categories:
- **Account**: Login, registration, password changes
- **Bank**: Deposit, withdraw, open bank
- **Character**: Create, select, delete characters
- **Item**: Pickup, drop, use, junk items
- **Player**: Movement, attacks, warps, chat
- **Npc**: NPC interactions, combat
- **Shop/Trade/Quest**: Commerce and questing

### Game Services (`src/Acorn/Game/Services/`)

Business logic services with interface-based design:

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IInventoryService` | `InventoryService` | Player inventory CRUD |
| `IBankService` | `BankService` | Bank storage operations |
| `IPaperdollService` | `PaperdollService` | Equipment management |
| `ILootService` | `LootService` | Drop calculations |
| `IStatCalculator` | `StatCalculator` | Stat/formula calculations |

### World State (`src/Acorn/World/`)

Thread-safe game world management:

- **WorldState**: Root container with concurrent dictionaries
  - `Maps`: `ConcurrentDictionary<int, MapState>`
  - `Players`: `ConcurrentDictionary<int, PlayerState>`
  - `GlobalMessages`: Cross-map chat/announcements

- **MapState**: Per-map state
  - Players currently on map
  - NPCs and their positions
  - Dropped items
  - Chests and other interactables

- **NpcState**: Individual NPC
  - Position and movement
  - Health and combat state
  - Behavior type (movement patterns)

- **PlayerState**: Connected player session
  - Network connection
  - Current character
  - Session data (trade, dialogue, etc.)

### Controllers (`src/Acorn/World/Services/`)

Higher-level orchestration services:

| Controller | Purpose |
|------------|---------|
| `IMapController` | Map-level operations, player enter/leave |
| `INpcController` | NPC spawning, movement, respawn |
| `IPlayerController` | Player actions, movement, combat |
| `IMapBroadcastService` | Send packets to all players on map |

### Caching (`src/Acorn.Shared/Caching/`)

Two-tier caching strategy:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
}
```

- **RedisCacheService**: Production distributed cache
- **InMemoryCacheService**: Development fallback

### Database (`src/Acorn.Database/`)

EF Core with multi-provider support:

- **AcornDbContext**: Main DbContext
- **Repositories**: `IAccountRepository`, `ICharacterRepository`
- **DbInitialiser**: Seeds default account, applies migrations

## Hosted Services

Background services running in the server:

| Service | Purpose |
|---------|---------|
| `WorldHostedService` | NPC movement ticks, world updates |
| `DropTableHostedService` | Loads drop tables from files |
| `NewConnectionHostedService` | Accepts new client connections |
| `PlayerPingHostedService` | Keep-alive pings |
| `PubFileCacheHostedService` | Caches pub files (items, npcs, etc.) |
| `MapCacheHostedService` | Caches map data |
| `WiseManQueueService` | Processes AI NPC chat queue |

## Data Files (`src/Acorn/Data/`)

Game data loaded at startup:

- `drops.txt`: NPC loot tables
- `news.txt`: Server news/MOTD
- `quests/`: Quest definitions

Pub files (loaded from game client data):
- `pub/dat001.eif`: Items
- `pub/dtn001.enf`: NPCs
- `pub/dsl001.esf`: Spells
- `pub/dat001.ecf`: Classes
