# Acorn — Codebase Review & Roadmap

> Comprehensive review of the Acorn Endless Online server emulator and a prioritized plan for next steps.
>
> Date: 2026-06-26 · Reviewed at commit `4a3da5b`
> **Updated: 2026-09-21** — re-audited against current `main`. Most of the Phase 0/2 roadmap has
> shipped; statuses below now reflect the code as it stands, with remaining gaps called out.

---

## 1. Executive Summary

Acorn is a from-scratch C# / .NET 11 reimplementation of an Endless Online (EO)
game server, in the lineage of EOSERV (C++) and reoserv (Rust). It is **well
beyond a prototype**: it has a working network stack (TCP + WebSocket), the full
EO login/handshake/encryption flow (verified by end-to-end integration tests),
multi-provider EF Core persistence, a tick-driven world simulation, and roughly
**150 packet handlers** covering most of the game's social and economy systems.

The architecture is clean and modern: interface-driven services, dependency
injection throughout, a clear project layering, OpenTelemetry metrics, and an
in-memory world model built on concurrent collections. A developer familiar with
EO could stand this up and log in today.

The gaps that remain are **depth items**, not core systems: spell casting, PvP
combat, gold drops, emotes and item-use feedback — all flagged as stubs in the
June review — are now implemented, with automated coverage (727 tests). The
open backlog is now: a packet soak/fuzz harness (1.3), quest-engine validation
against real EO quest files (3.3), map-effect parity audit (3.4), the remaining
eoserv admin commands (`$dress`/`$strip`/`$request`, privilege toggles),
multi-part pub file splitting, world-tick sharding, and load testing (Phase 4).

**Overall grade: solid, maintainable foundation with core combat closed (~A-).**

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
| Player → NPC combat, drops, XP, level-up | ✅ | Item **and gold** drops, XP, level-up, quest kill hooks |
| Inventory / paperdoll / weight | ✅ | |
| Bank / locker / chest | ✅ | |
| Shop (buy/sell/create) | ✅ | |
| Trade (player-to-player) | ✅ | |
| Guilds | ✅ | 15 handlers incl. offline kick & rank updates (persisted via `GuildMembers`) |
| Party | ✅ | |
| Quests | ✅ | Use/list/accept + `QuestService` |
| Citizen / Inn | ✅ | Sleep restores HP/TP, charges gold and warps to the inn's sleep area |
| Marriage / Priest / Wedding ceremony | ✅ | Tick-driven ceremony |
| Board, Barber, Jukebox, Chairs, Doors | ✅ | |
| Admin commands & moderation | ✅ (rich) | warp, ban, jail, mute, spawn, set, remap, shutdown, undress, … |
| WiseMan AI NPC (Gemini) | ✅ (optional) | Feature-flagged |
| Arena | ✅ | Queue leave fixed; arena PvP scoring via `ArenaService` |
| REST API (online players, maps, pub) | ✅ | Minimal-API project; guild name/rank included in online cache |
| **Spell casting (attack/heal/buff)** | ✅ | `ISpellCastService`: chant timer, TP cost, self/other/group targeting |
| **Player-vs-player combat** | ✅ | `AttackUseClientPacketHandler.HandlePlayerAttack` on PK maps + arena, with death/respawn |
| **Item-use effects → client** | ✅ | `RecoverAgree`/inventory replies sent after use |
| Emote reporting | ✅ | Validated, range-limited broadcast (eoserv `Emote_Report` parity) |

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
| Attack | ✅ | NPC melee **and PvP** (PK-map/arena gating, backstab, ranged, death) |
| Spell | ✅ | Self/other/group casting via `ISpellCastService` |
| Item, Paperdoll, Bank, Locker, Chest, Shop, Trade | ✅ | Item *use* effects echo to the client |
| Character, StatSkill, Players, Talk, Global, Party, Guild | ✅ | Talk/admin command surface is rich |
| Bank, Barber, Board, Book, Citizen, Jukebox, Quest | ✅ | Book replies with full details; Citizen sleeps + warps |
| Emote | ✅ | Range-limited broadcast, client-safe emote whitelist |
| Message | ✅ | ping answered with pong |
| AdminInteract | ✅ | report/tell |

**Acorn additionally has** systems eoserv folds elsewhere or lacks as discrete
handlers: dedicated **Marriage/Priest** + tick-driven wedding, **Arena**,
**Npc/Range** request handlers, and the **WiseMan (Gemini) AI NPC**.

**Takeaway:** category coverage is *not* the gap. All four intra-family holes
flagged in June — **Spell, PvP Attack, Emote, item-use feedback** — are now
closed, so Acorn is at functional parity with eoserv's handler families.

### 2A.2 Admin/player commands — the real coverage gap

eoserv's `admin.ini` defines ~70 commands across access levels 1–4. Acorn
implements the high-frequency moderation and debug set, and elegantly collapses
eoserv's ~25 `setX` commands into one generic `$set <player> <attr> <value>`
(supports admin, class, gender, level, exp, hp/maxhp, tp/maxtp, sp/maxsp, skin…).

**Covered:** info/player, inventory, kick, jail, free (unjail), ban, mute/unmute,
freeze/unfreeze, warp, wmt/warpmeto (go to player), summon/bring/warptome (pull
player to you), who/online, hide, evacuate, quake, set (≈ the whole `setX` family),
spawnitem (sitem/si), spawnnpc (snpc), addspell (spell), global, help, location,
usage, uptime, rehash (config re-read), repub (pub reload), item/npc/spellinfo/
class lookups, qstate, and the silent variants (skick/sjail/sban/smute). Per-command
minimum admin levels are now declared on the handlers (`RequiredLevel`) and enforced
by the dispatcher; `$set admin` additionally requires HighGameMaster. Jail, freeze
and bans are persisted (jail/freeze on the character, bans in the database).

**Implemented since the June review:** `$remap <map>` (hot-reload one map file;
refuses while players are inside), `$shutdown [reason]` (announces and stops the
host gracefully, persisting all online characters first), and `$undress <player>`
(force-unequip into inventory).

**Missing vs eoserv (candidate backlog):**

| Command(s) | Purpose | Priority |
|---|---|---|
| `dress` / `strip` | Re-equip after `$undress`; item confiscation | Low |
| `request` | Toggle per-player request logging | Low |
| Privilege flags: `nowall`, `seehide`, `killnpc`, `cmdprotect`, `unlimitedweight` | GM toggles | Low |

None of these are gameplay-critical. The high-frequency GM warp tools and the
uptime/`repub`/`rehash`/`remap`/`shutdown` set are now implemented; see
[COMMANDS.md](COMMANDS.md) for the full command reference.

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

### 4.1 Correctness bugs — resolved

All four bugs flagged in June are fixed in current code:

- ✅ `EmoteReportClientPacketHandler` now broadcasts range-limited emotes and
  validates the emote id against the client-safe whitelist (mirrors eoserv
  `Emote_Report`).
- ✅ `MapState.LeaveArenaQueue` rebuilds the queue preserving order.
- ✅ Item-use effects send `RecoverAgree`/inventory replies so the client UI
  matches server state.
- ⚠️ `NpcCombatService` direction mapping (`(0,1) => Up` from `npc.X - target.X`)
  is still **unverified** — it needs a pass with a real client to confirm NPCs
  face the player they hit.

### 4.2 Protocol fragility (watch closely)

The recent commit history was dominated by sequence/encryption fixes
(`align sequence handling with reoserv`, `replace SDK sequencer with
pre-increment`, `avoid session IDs that collide with AccountReply enum values`).
The behaviours are now locked in by integration tests (`LoginFlowTests`,
`PacketSequenceTests`, `WalkTimestampTests`, connection/handshake coverage), so
regressions surface in CI rather than during manual client testing. Keep adding
sequence assertions for any new hard-won protocol behaviour.

### 4.3 Test coverage

Coverage grew from ~3 unit-test files to **727 tests** (unit + integration, all
green in CI): combat, loot, stats, quests, guilds, trade, party, map/range
logic, admin services, ping policy, sessions and shutdown persistence all have
automated coverage. Remaining thin spots: `WeightCalculator`, inn/citizen data
loaders, and the WiseMan Gemini queue (network-bound, hard to assert).

### 4.4 Documentation drift — resolved

`AGENTS.md` and the `.ai/` context describe the current architecture (in-memory
caching, no `Acorn.Domain`, no live Redis tier). `docs/SHOPS.md` was added and
`docs/COMMANDS.md` stays current with the command surface, including the
`$remap`/`$shutdown`/`$undress` additions.

### 4.5 Smaller items

- `WorldHostedService.OnTick` is `async void` (acceptable for a timer handler,
  but exceptions only survive because of the try/catch — keep that invariant).
- `GuildService` offline kick/rank updates are implemented: they operate on the
  persisted `GuildMembers` row and surface via `SendGuildReply` (see
  `GuildServiceOfflineTests`).
- The online-character realtime cache now includes guild name/rank (previously
  hardcoded empty).
- **Open:** `WelcomeAgreeClientPacketHandler` echoes the requested pub file id
  but always sends the whole file; multi-part pub splitting is unsupported
  (matches reoserv's TODO) and unknown file types still throw
  `NotImplementedException` — a malformed client could force an exception path.

---

## 5. Roadmap

Legend: ✅ done · ⏳ open · 🟡 partial. Effort is rough: S < 1 day, M = 1–3 days, L = a week+.

### Phase 0 — Stabilize ✅ complete

| # | Task | Effort |
|---|------|--------|
| 0.1 | ✅ Fix `EmoteReportClientPacketHandler` (broadcast or no-op, never throw) | S |
| 0.2 | ✅ Fix `LeaveArenaQueue` to actually replace the queue | S |
| 0.3 | ✅ Send `RecoverAgree`/inventory packets after item use so the client UI matches server state | S |
| 0.4 | ✅ Reconcile docs: remove Redis/`Acorn.Domain` from `AGENTS.md` & `.ai/context`, drop the dead `REDIS_REALTIME.md` link | S |
| 0.5 | ✅ Audit & document the sequence/encryption invariants the recent fixes established (locked in by `PacketSequenceTests` + login/handshake integration tests) | S |

### Phase 1 — Lock down the protocol & raise coverage 🟡

| # | Task | Effort |
|---|------|--------|
| 1.1 | ✅ Extend integration tests: walk/warp/attack/map-interaction/sit-stand/sequence/timestamp suites now run against the real server | M |
| 1.2 | 🟡 Unit tests for `StatCalculator`, `LootService`, quest progression ✅; `WeightCalculator` still untested | M |
| 1.3 | ⏳ Add a packet fuzz/soak test that drives many randomized valid packets to surface sequencing desync | M |
| 1.4 | ✅ CI runs `dotnet test` on every PR (failures block merge) | S |

### Phase 2 — Close core combat gaps ✅ complete

| # | Task | Effort |
|---|------|--------|
| 2.1 | ✅ **Spell casting** end-to-end: chant timer, TP cost, attack/heal/buff spells, self/other/group (`ISpellCastService` + `SpellTarget*` handlers) | L |
| 2.2 | ✅ **Player-vs-player combat** on PK maps + arena (party protection, hidden-target rules, death/respawn via `PlayerController.DieAsync`) | M |
| 2.3 | ✅ **NPC gold drops** via the unified loot roll (gold = item 1) with separate `NpcGoldDropped` metrics | S |
| 2.4 | ✅ Item-use effect types: heal, cure-curse, EXP reward, effect potions, hair dye, alcohol, teleport scrolls using real INN data | M |
| 2.5 | ✅ Back/side-stab bonus, configurable first-hit critical, ranged distance for bows (`AttackTrace`); arrow ammo consumption not modelled (client-side) | M |

### Phase 3 — Depth & polish 🟡

| # | Task | Effort |
|---|------|--------|
| 3.1 | ✅ Guild offline operations — kick/rank update persist the `GuildMembers` row (`GuildServiceOfflineTests`) | M |
| 3.2 | ✅ Citizen/Inn sleep-warp (HP/TP restore, gold charge, warp to inn sleep area) and home registration/removal | S |
| 3.3 | ⏳ Quest engine breadth: validate against a meaningful set of real EO quest files | L |
| 3.4 | ⏳ Map effects parity (spikes/timed spikes, lava, healing tiles) audit | M |
| 3.5 | ✅ Book full reply; `MessagePing` answered with pong; guild fields in the online-character cache | S |
| 3.6 | 🟡 Admin parity vs eoserv (§2A.2): `remap`, `shutdown`, `undress` added; `dress`/`strip`/`request` and privilege flags remain | M |

### Phase 4 — Scale & operability 🟡

| # | Task | Effort |
|---|------|--------|
| 4.1 | ⏳ Profile and, if needed, shard the single world tick (per-map or partitioned tasks) for many-map/many-player loads | M |
| 4.2 | ✅ Graceful shutdown that persists all online characters — `ShutdownPersistenceHostedService` runs on host stop (SIGTERM or `$shutdown`) before listeners close | S |
| 4.3 | ⏳ Admin/ops dashboard surface via the existing REST API + metrics | M |
| 4.4 | ⏳ Load-test harness driving N synthetic clients through the real protocol | M |

---

## 6. Suggested Immediate Next Steps

The original June shortlist (Phase 0, the protocol test lock-in, all of Phase 2,
and most of Phase 3) has shipped. Remaining, in rough priority order:

1. **1.3 packet soak/fuzz harness** — sequencing is the historically fragile
   subsystem and the only untested-on-randomized-input path.
2. **3.3 quest breadth validation** — the quest engine is structurally complete
   but unproven against real content; this gates "content-ready" status.
3. **3.4 map effects audit** — spikes/lava/healing tiles parity with eoserv.
4. **4.1 world-tick profiling + 4.4 load harness** — before taking real
   population, know where the single-tick model breaks.
5. Small leftovers: `WeightCalculator` unit tests (1.2), `$dress`/`$strip`/
   `$request` (3.6), and replacing the `NotImplementedException` for unsupported
   pub file types in `WelcomeAgreeClientPacketHandler` with a graceful reply.
