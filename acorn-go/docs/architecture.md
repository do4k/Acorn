# Acorn-Go Architecture

Go implementation of the Acorn Endless Online server emulator.

## Project Structure

```
acorn-go/
├── cmd/server/main.go           # Entry point, signal handling
├── internal/
│   ├── config/config.go         # YAML configuration loading
│   ├── server/
│   │   ├── server.go            # Server orchestrator, handler registration
│   │   ├── tcp.go               # TCP listener (port 8078)
│   │   ├── websocket.go         # WebSocket listener (port 8079)
│   │   └── readloop.go          # Shared packet read loop
│   ├── session/
│   │   ├── session.go           # Per-connection state, send/receive
│   │   ├── manager.go           # Concurrent session tracking, ID generation
│   │   └── state.go             # ClientState enum
│   ├── net/
│   │   ├── codec.go             # EO protocol framing + encryption
│   │   ├── router.go            # Packet dispatch by (family, action)
│   │   └── handler.go           # Handler interface
│   ├── handler/
│   │   ├── init.go              # Init handshake (encryption negotiation)
│   │   ├── connection.go        # Connection accept, ping/pong, PingService
│   │   ├── account.go           # Account_Request + Account_Create
│   │   ├── login.go             # Login_Request with character list
│   │   ├── character.go         # Character Request/Create/Take/Remove
│   │   ├── welcome.go           # Welcome Request/Msg/Agree (file transfer)
│   │   └── helpers.go           # Name validation, character list builder
│   ├── database/
│   │   ├── database.go          # SQLite via modernc.org/sqlite, schema migration
│   │   └── repositories.go     # Account/Character CRUD, password hashing
│   └── world/
│       ├── world.go             # World state, map container, tick loop
│       ├── map_state.go         # Per-map state (players, NPCs, ground items)
│       ├── pub.go               # Pub/map file loading (EIF/ENF/ESF/ECF/EMF)
│       └── protocol_helpers.go  # Int-to-protocol-type conversions
├── config.yaml                  # Default configuration
├── nginx.conf                   # Example nginx reverse proxy
├── Dockerfile                   # Multi-stage Docker build
├── docker-compose.yml           # Single-service compose for deployment
└── .github/workflows/ci.yml    # GitHub Actions CI (build, vet, test, docker)
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `github.com/ethanmoffat/eolib-go/v3` | EO protocol: packet structs, encoding, encryption, sequencing |
| `github.com/coder/websocket` | WebSocket server (nhooyr.io fork) |
| `modernc.org/sqlite` | Pure-Go SQLite driver (no CGO) |
| `gopkg.in/yaml.v3` | Configuration parsing |

## Connection Lifecycle

```
Client                          Server
  │                               │
  │──── TCP/WS Connect ──────────>│  State: Uninitialized
  │                               │  Generate session ID
  │                               │
  │──── Init_Init ───────────────>│  Client sends challenge + version + HDID
  │<─── Init_Init ────────────────│  Server replies: seq, multiples, hash
  │                               │  State: Initialized
  │                               │  Encryption now active
  │                               │
  │──── Connection_Accept ───────>│  Client echoes multiples + player ID
  │                               │  State: Accepted
  │                               │
  │  ... (login, char select) ... │
  │                               │
  │<─── Connection_Player ────────│  Periodic ping (every 8s)
  │──── Connection_Ping ─────────>│  Client pong response
  │                               │
```

## EO Protocol Wire Format

Each packet on the wire is:

```
[len1][len2][action][family][sequence?][...payload...]
```

- **Length prefix** (2 bytes): EO-number-encoded length of everything after it
- **Action** (1 byte): `PacketAction` enum value
- **Family** (1 byte): `PacketFamily` enum value
- **Sequence** (1-2 bytes): Present after init handshake, EO char or EO short
- **Payload**: Packet-specific data using EO number encoding

### Encryption

After the Init handshake negotiates two random multiples (6-12):

**Decrypting client packets:** `FlipMSB -> Deinterleave -> SwapMultiples(clientMulti)`

**Encrypting server packets:** `SwapMultiples(serverMulti) -> Interleave -> FlipMSB`

Init packets (family=255, action=255) are never encrypted.

### Number Encoding

EO uses a custom base-253 encoding that avoids bytes `0x00` (reserved) and `0xFF` (string delimiter):

| Type | Bytes | Max Value |
|------|-------|-----------|
| Char | 1 | 253 |
| Short | 2 | 64,009 |
| Three | 3 | 16,194,277 |
| Int | 4 | ~4 billion |

## Key Design Decisions

### Transport Abstraction

Both TCP and WebSocket connections produce an `io.ReadWriteCloser` that wraps the underlying transport. The `Session` struct is transport-agnostic — it only knows about reading/writing bytes.

The WebSocket adapter (`wsReadWriteCloser`) maps WebSocket binary messages to a byte stream, handling message boundaries transparently.

### Session Model

Each connection gets a `Session` with:
- Unique session ID (random, 8 to SHORT_MAX, avoids protocol reply code collisions)
- `Codec` for per-connection encryption state
- `PacketSequencer` for sequence validation
- Atomic `ClientState` for lock-free state checks
- Mutex-protected writes (only one goroutine writes at a time)

### Packet Routing

The `Router` maps `(family, action)` pairs to `Handler` implementations using a simple map lookup. Handlers are registered at startup in `server.go`. Unhandled packets are silently dropped (matching eoserv behavior).

### No DI Container

Go's explicit wiring replaces C#'s DI container. Dependencies are passed as function arguments or struct fields. This makes the dependency graph visible in code and avoids reflection overhead.

## Adding a New Packet Handler

1. Create a struct implementing `net.Handler` in `internal/handler/`
2. Register it in `server.registerHandlers()` with the correct family/action
3. Access session state, send responses via `session.Send()`

Example:

```go
type MyHandler struct{}

func (h *MyHandler) Handle(s *session.Session, pkt eonet.Packet) error {
    myPkt := pkt.(*client.SomeClientPacket)
    // Process...
    return s.Send(&server.SomeServerPacket{...})
}
```

## Implemented Packet Handlers

### Account Flow (two-step)

1. **Account_Request** (`handler/account.go`) -- Client sends username; server checks availability, returns session ID as `reply_code` (> 9, hits Default case).
2. **Account_Create** -- Client sends username + hashed password + session ID; server creates account.

### Login Flow

- **Login_Request** (`handler/login.go`) -- Validates credentials, returns character list or error code.

### Character Flow

- **Character_Request** (`handler/character.go`) -- Request to create; returns `CreateID=1000` (Default case).
- **Character_Create** -- Name/gender/race/hairstyle, creates character in DB.
- **Character_Take** -- Delete request; returns character name for confirmation.
- **Character_Remove** -- Delete confirmation; removes character from DB.

### Welcome Flow (enter game)

1. **Welcome_Request** (`handler/welcome.go`, sub-id 1) -- Select character; returns stats, settings, RIDs, file sizes.
2. **Welcome_Msg** (sub-id 2) -- Enter game; returns news, inventory, spells, nearby players/NPCs/items.
3. **Welcome_Agree** -- File transfer requests; serves EMF/EIF/ENF/ESF/ECF files.

### Connection Maintenance

- **Connection_Accept** (`handler/connection.go`) -- Client echoes init multiples; state -> Accepted.
- **Connection_Ping** -- Pong response to server keep-alive.
- **PingService** -- Background goroutine sends pings every N seconds, disconnects unresponsive clients.

## Database

SQLite with WAL mode via `modernc.org/sqlite` (pure Go, no CGO). Schema auto-migrated on startup.

**Tables:** `accounts`, `characters`, `character_inventory`, `character_spells`, `character_equipment`.

**Password hashing:** SHA-256 with per-account random salt (`salt + username + password`). Wire-compatible with eoserv but more secure due to per-account salt.

## World State

- **World** (`world/world.go`) -- Holds all map states, pub file data, tick loop (50ms).
- **MapState** (`world/map_state.go`) -- Per-map: players, NPCs, ground items, NearbyInfo builder.
- **Pub files** (`world/pub.go`) -- Loads EIF/ENF/ESF/ECF via eolib-go `Deserialize`, raw bytes cached for file transfer.

## Docker

Multi-stage build: `golang:1.25-alpine` builder -> `alpine:3.21` runtime. Runs as non-root `acorn` user. Game data and DB volume-mounted.

## Next Steps

- [ ] Player movement and position tracking
- [ ] Chat (public, private, global, admin)
- [ ] NPC AI (movement, combat, drops)
- [ ] Player combat (attack, damage, death/respawn)
- [ ] Shops and quest systems
- [ ] Trade between players
- [ ] Party system
- [ ] Guild system
