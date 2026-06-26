# Acorn — Codebase Review & Roadmap

> Comprehensive review of the Acorn Endless Online server emulator and a prioritized plan for next steps.
>
> Date: 2026-06-26 · Reviewed at commit `4a3da5b`

---

## 1. Executive Summary

Acorn is a from-scratch C# / .NET 10 reimplementation of an Endless Online (EO)
game server, in the lineage of EOSERV (C++) and reoserv (Rust). It is **well
beyond a prototype**: it has a working network stack (TCP + WebSocket), the full
EO login/handshake/encryption flow (verified by end-to-end integration tests),
multi-provider EF Core persistence, a tick-driven world simulation, and roughly
**150 packet handlers** covering most of the game's social and economy systems.

The architecture is clean and modern: interface-driven services, dependency
injection throughout, a clear project layering, OpenTelemetry metrics, and an
in-memory world model built on concurrent collections. A developer familiar with
EO could stand this up and log in today.

The gaps are concentrated in **core combat depth** rather than infrastructure.
Spell casting is entirely stubbed, player-vs-player combat does not exist, item
"use" effects are partially wired but never echoed to the client, and a handful
of handlers are incomplete or will throw. The biggest non-feature risks are
**low automated test coverage relative to surface area**, **lingering protocol
fragility** (recent history is dominated by sequence/encryption fixes), and
**documentation drift** (AGENTS.md still describes Redis and an `Acorn.Domain`
project that no longer exist).

**Overall grade: solid, maintainable foundation (~B+). The path forward is depth
and hardening, not a rewrite.**

---

## 2. What Exists Today

### 2.1 Architecture & Layering

```
Acorn.Shared (utilities, caching, options)  ─┐
Acorn.Database (EF Core, repositories)       ─┼─► Acorn      (game server)
                                              └─► Acorn.Api  (REST query API)
```

- **Network layer** — `TcpListenerHostedService` + `WebSocketListenerHostedService`
  feed a shared `ConnectionHandler`. Transport is abstracted behind
  `ICommunicator` (`TcpCommunicator` / `WebSocketCommunicator`), so handlers are
  transport-agnostic. Packet (de)serialization uses `Moffat.EndlessOnline.SDK`.
- **Packet handlers** — `IPacketHandler<TPacket>` implementations auto-registered
  by `AddPacketHandlers()`, organized by domain (Account, Bank, Guild, Trade,
  Quest, …). A `[RequiresCharacter]` attribute gates handlers that need a
  logged-in character.
- **World model** — `WorldState` (root, concurrent dictionaries) → `MapState`
  (per-map players/NPCs/items/chests/doors) → `NpcState` / `PlayerState`. A
  single `WorldHostedService` timer drives `MapState.Tick()` across all maps.
- **Game services** — `IInventoryService`, `IBankService`, `IPaperdollService`,
  `ILootService`, `IStatCalculator`, plus higher-level controllers
  (`IMapController`, `INpcController`, `IPlayerController`).
- **Persistence** — `AcornDbContext` over SQLite / MySQL / PostgreSQL / SQL
  Server; repositories with an in-memory cache layer; `DbInitialiser` seeds the
  default `acorn`/`acorn` account.
- **Observability** — `AcornMetrics` exports counters/histograms (NPC kills,
  XP, level-ups, map tick duration) via OpenTelemetry OTLP.

### 2.2 Feature Inventory

| Area | Status | Notes |
|------|--------|-------|
| Login / handshake / encryption | ✅ Working | Covered by TCP + WS integration tests |
| Account create / password change | ✅ | |
| Character create / select / delete | ✅ | |
| Movement & map transitions / warps | ✅ | |
| NPC AI (wander, aggro, melee attack) | ✅ | Spawn-type movement rates per EOSERV |
| Player → NPC combat, drops, XP, level-up | ✅ | Item drops work; **gold drops TODO** |
| Inventory / paperdoll / weight | ✅ | |
| Bank / locker / chest | ✅ | |
| Shop (buy/sell/create) | ✅ | |
| Trade (player-to-player) | ✅ | |
| Guilds | ✅ (broad) | 15 handlers; offline kick/rank are TODO |
| Party | ✅ | |
| Quests | ✅ | Use/list/accept + `QuestService` |
| Citizen / Inn | ◑ | Sleep-warp on accept is TODO |
| Marriage / Priest / Wedding ceremony | ✅ | Tick-driven ceremony |
| Board, Barber, Jukebox, Chairs, Doors | ✅ | |
| Admin commands & moderation | ✅ (rich) | warp, ban, jail, mute, spawn, set, … |
| WiseMan AI NPC (Gemini) | ✅ (optional) | Feature-flagged |
| Arena | ◑ | Spawns work; queue removal is buggy (see 4.1) |
| REST API (online players, maps, pub) | ✅ | Minimal-API project |
| **Spell casting (attack/heal/buff)** | ❌ **Stub** | All `SpellTarget*` handlers are TODO |
| **Player-vs-player combat** | ❌ Missing | `AttackUse` only targets NPCs |
| **Item-use effects → client** | ◑ | HP/TP changed server-side but no packet sent |
| Emote reporting | ❌ Throws | `EmoteReportClientPacketHandler` throws |

Legend: ✅ implemented · ◑ partial · ❌ missing/broken

---

## 2A. eoserv Parity (Handlers + Commands)

Compared against the reference implementation
[eoserv/eoserv](https://github.com/eoserv/eoserv) `master` — specifically its
`src/handlers/*.cpp` packet-handler families and its `admin.ini` command set.

### 2A.1 Packet-handler families — essentially at parity

eoserv ships **38 client-facing handler families** (a 39th, `Internal.cpp`, is
eoserv's server-to-server bus and not a client protocol). **Acorn implements all
38 families.** The differences are *within* families, not missing categories:

| eoserv family | Acorn | Note |
|---|---|---|
| Account, Login, Connection, Init, Welcome, Refresh | ✅ | Login/handshake fully covered + tested |
| Walk, Warp, Face, Sit, Chair, Door | ✅ | |
| Attack | ◑ | NPC combat only — **no PvP** (eoserv supports PK maps) |
| Spell | ❌ | eoserv casts attack/heal/group spells; Acorn's are stubs |
| Item, Paperdoll, Bank, Locker, Chest, Shop, Trade | ✅ | Item *use* effects partial (see §4.1) |
| Character, StatSkill, Players, Talk, Global, Party, Guild | ✅ | Talk/admin command surface is rich |
| Bank, Barber, Board, Book, Citizen, Jukebox, Quest | ✅/◑ | Book request is a stub; Citizen sleep-warp TODO |
| Emote | ❌ | eoserv broadcasts emotes; Acorn's handler throws |
| Message | ◑ | ping/pong not answered |
| AdminInteract | ✅ | report/tell |

**Acorn additionally has** systems eoserv folds elsewhere or lacks as discrete
handlers: dedicated **Marriage/Priest** + tick-driven wedding, **Arena**,
**Npc/Range** request handlers, and the **WiseMan (Gemini) AI NPC**.

**Takeaway:** category coverage is *not* the gap. The eoserv diff confirms the
same four intra-family holes my review already flagged — **Spell, PvP Attack,
Emote, item-use feedback** — which is reassuring corroboration that the roadmap
is aimed at the right targets.

### 2A.2 Admin/player commands — the real coverage gap

eoserv's `admin.ini` defines ~70 commands across access levels 1–4. Acorn
implements the high-frequency moderation and debug set, and elegantly collapses
eoserv's ~25 `setX` commands into one generic `$set <player> <attr> <value>`
(supports admin, class, gender, level, exp, hp/maxhp, tp/maxtp, sp/maxsp, skin…).

**Covered:** info/player, inventory, kick, jail, free (unjail), ban, mute/unmute,
freeze/unfreeze, warp, hide, evacuate, quake, set (≈ the whole `setX` family),
spawnitem (sitem/ditem), spawnnpc (snpc/dnpc), addspell (learn), global,
location, usage.

**Missing vs eoserv (candidate backlog):**

| Command(s) | Purpose | Priority |
|---|---|---|
| `warptome` / `warpmeto` | Pull a player to you / go to a player | High (common GM tool) |
| `uptime` | Server uptime readout | Low |
| `remap` | Hot-reload a single map | Med |
| `shutdown`, `rehash`, `repub`, `request` | Server control / config & pub reload | Med |
| `strip` / `dress` / `undress` / `dress2` | Force-equip/unequip a player | Low |
| `qstate` | Inspect/force a player's quest state | Med (debug) |
| `item`/`npc`/`spell`/`class`/`paperdoll`/`book` | In-game data lookups | Low |
| Privilege flags: `nowall`, `seehide`, `killnpc`, `cmdprotect`, `unlimitedweight` | GM toggles | Low |
| Silent variants: `skick`, `sjail`, `sban`, `smute` | Act without announcing | Low |

None of these are gameplay-critical, but `warptome`/`warpmeto` and the
server-control trio (`rehash`/`repub`/`shutdown`) are the ones operators will
miss first.

---

## 3. Strengths

1. **Clean, testable design.** Interfaces + DI everywhere; logic lives in
   services rather than handlers, so it can be unit-tested without a socket.
2. **Real end-to-end coverage of the hardest part.** The init/encryption/
   sequence handshake is notoriously fiddly in EO, and there are *integration*
   tests (`LoginFlowTests`) that spin up the real server over both TCP and WS.
3. **Breadth of features.** Most social/economy systems an EO server needs are
   present, not just the happy-path login.
4. **Operational maturity.** Multi-DB support, Docker/compose profiles,
   multi-arch image publishing, OpenTelemetry, GitHub Actions CI.
5. **Good contributor onboarding.** `AGENTS.md` + `.ai/` context and prompt
   templates lower the barrier for both humans and agents.

---

## 4. Issues & Risks

### 4.1 Correctness bugs (fix soon)

- **`EmoteReportClientPacketHandler` throws `NotImplementedException`.** Any
  client emote packet will raise an unhandled exception in the handler pipeline.
  At minimum it should no-op or broadcast the emote.
- **`MapState.LeaveArenaQueue` is a no-op.** It builds a filtered `newQueue` but
  never assigns it back to `ArenaQueue`, so players never actually leave the
  queue. (The code even comments the operation as "racy but acceptable" — but it
  does nothing at all.)
- **Item-use effects are invisible to the client.** `HandleHealItem` etc. mutate
  `Character.Hp/Tp` but the `// TODO: Send updated inventory packet` /
  `RecoverAgree` broadcasts are never sent, so the client UI desyncs from server
  state after using a potion.
- **`NpcCombatService` direction mapping looks inverted.** `(0,1) => Up` is
  derived from `npc.X - target.X`; this should be verified against a real client
  to confirm NPCs face the player they hit.

### 4.2 Protocol fragility (watch closely)

The recent commit history is almost entirely sequence/encryption fixes
(`align sequence handling with reoserv`, `replace SDK sequencer with
pre-increment`, `avoid session IDs that collide with AccountReply enum values`,
`regenerate seeded password hash for .NET 10`). This subsystem works today but is
clearly delicate. **Recommendation:** expand integration tests to lock in the
behaviors that were hard-won (sequence progression across many packets, ping/
pong cadence, reconnect), so regressions are caught automatically rather than by
manual client testing.

### 4.3 Test coverage gap

Only three unit-test files (`Character`, `BankService`, `InventoryService`) plus
the login integration tests, against ~150 handlers and dozens of services.
Combat, loot, stat calculation, quests, guilds, and trade have no automated
coverage. Given how much game logic is pure (formula/inventory/stat math), this
is low-hanging fruit with high payoff.

### 4.4 Documentation drift

- `AGENTS.md` lists an **`Acorn.Domain`** project and a **Redis** caching tier;
  neither exists (Redis was removed in `4794e28`, domain models live in
  `Acorn.Database/Models` and `Acorn/Game/Models`).
- `AGENTS.md` links `docs/REDIS_REALTIME.md`, which is not present.
- `.ai/context/architecture.md` still documents `RedisCacheService`.

This misleads new contributors and agents. It should be reconciled in one pass.

### 4.5 Smaller items

- `WorldHostedService.OnTick` is `async void` (acceptable for a timer handler,
  but exceptions only survive because of the try/catch — keep that invariant).
- Several `await Task.CompletedTask` placeholders mark handlers that don't yet do
  async work; harmless but signal unfinished logic.
- `GuildService` has `// TODO: offline kick` / `offline rank update` — guild
  operations on offline members are silently skipped.

---

## 5. Roadmap

Phased so each item is independently shippable. Effort is rough: S < 1 day,
M = 1–3 days, L = a week+.

### Phase 0 — Stabilize (do first)

| # | Task | Effort |
|---|------|--------|
| 0.1 | Fix `EmoteReportClientPacketHandler` (broadcast or no-op, never throw) | S |
| 0.2 | Fix `LeaveArenaQueue` to actually replace the queue | S |
| 0.3 | Send `RecoverAgree`/inventory packets after item use so the client UI matches server state | S |
| 0.4 | Reconcile docs: remove Redis/`Acorn.Domain` from `AGENTS.md` & `.ai/context`, drop the dead `REDIS_REALTIME.md` link | S |
| 0.5 | Audit & document the sequence/encryption invariants the recent fixes established | S |

### Phase 1 — Lock down the protocol & raise coverage

| # | Task | Effort |
|---|------|--------|
| 1.1 | Extend integration tests: character create→enter-game→walk, multi-packet sequence progression, ping/pong, disconnect cleanup | M |
| 1.2 | Unit tests for `StatCalculator`, `LootService`, `WeightCalculator`, quest progression | M |
| 1.3 | Add a packet fuzz/soak test that drives many randomized valid packets to surface sequencing desync | M |
| 1.4 | Wire test execution into CI gating (it already runs; make failures block merge) | S |

### Phase 2 — Close core combat gaps

| # | Task | Effort |
|---|------|--------|
| 2.1 | **Implement spell casting** end-to-end: chant timer, TP cost, attack spells (damage NPC/player), heal/buff spells, group targeting. Replace the four `SpellTarget*` TODO stubs with a `MapState.CastSpell` path mirroring the melee flow | L |
| 2.2 | **Player-vs-player combat** in `AttackUse` (respect map PK flags / safe zones) and player death/respawn for PvP | M |
| 2.3 | **NPC gold drops** alongside item drops in `AttackUseClientPacketHandler` | S |
| 2.4 | Finish item-use effect types (cure curse, EXP scrolls, stat-reset) and home/inn teleport using real INN data | M |
| 2.5 | Critical-hit / back-stab and arrows/ranged where applicable | M |

### Phase 3 — Depth & polish

| # | Task | Effort |
|---|------|--------|
| 3.1 | Guild offline operations (kick/rank update for offline members) | M |
| 3.2 | Citizen/Inn sleep-warp and home registration | S |
| 3.3 | Quest engine breadth: validate against a meaningful set of real EO quest files | L |
| 3.4 | Map effects parity (spikes/timed spikes, lava, healing tiles) audit | M |
| 3.5 | Book/`MessagePing` and remaining minor handler TODOs | S |
| 3.6 | Admin command parity vs eoserv (§2A.2): add `warptome`/`warpmeto` first, then `rehash`/`repub`/`shutdown`, then the lower-priority lookups/flags | M |

### Phase 4 — Scale & operability

| # | Task | Effort |
|---|------|--------|
| 4.1 | Profile and, if needed, shard the single world tick (per-map or partitioned tasks) for many-map/many-player loads | M |
| 4.2 | Graceful shutdown that persists all online characters (not just on disconnect) | S |
| 4.3 | Admin/ops dashboard surface via the existing REST API + metrics | M |
| 4.4 | Load-test harness driving N synthetic clients through the real protocol | M |

---

## 6. Suggested Immediate Next Steps

If picking up work right now, start with **Phase 0** in a single PR (all small,
all low-risk, immediately improves correctness and contributor trust), then open
a dedicated effort for **2.1 (spell casting)** since it is the single largest
visible gap between Acorn and a feature-complete EO server.

Recommended order: `0.1 → 0.2 → 0.3 → 0.4 → 0.5`, then `1.1`/`1.2` in parallel
with starting `2.1`.
