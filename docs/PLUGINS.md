# Plugin Architecture — Feasibility Investigation & Design Proposal

> **Status:** Phase 0 implemented — contracts (`src/Acorn.Plugins`), ALC loader,
> manifest/config discovery, lifecycle, world/map tick hooks, the `#command` bridge,
> the `samples/HelloAcorn` plugin and tests are in (see §8 implementation notes).
> Everything beyond Phase 0 is design only.
> **Goal:** Let server authors package, share and config-enable "server mods" that hook
> into the world tick, combat and other character entry points — while EOSERV-parity
> gameplay stays in the core.

## TL;DR

**This is very feasible, and Acorn is unusually well-suited for it.** The codebase is
already built on the exact patterns a plugin host needs:

- A single serialised tick loop (`WorldHostedService` → `MapState.Tick`) — one obvious
  place to raise world/map tick hooks.
- Interface-based services behind DI (`IPlayerController`, `IMapController`,
  `IFormulaService`, …) — stable seams to intercept.
- Assembly-scanned, collection-resolved extension points (`AddPacketHandlers()`,
  `AddAllOfType<ITalkHandler>()`, `IEnumerable<ICommandHandler>` dispatch in
  `TalkReportClientPacketHandler`) — plugins can feed these same collections.
- Runtime NPC spawning already works (`SpawnNpcCommandHandler`), which is the core
  primitive pets/dungeon mobs need.

The .NET side is a solved problem: `AssemblyLoadContext` (ALC) +
`AssemblyDependencyResolver` is the documented, supported plugin pattern (Microsoft's
"Create a .NET Core application with plugins" tutorial, updated Feb 2026). The mature
third-party option is `DotNetCorePlugins` (McMaster.NETCore.Plugins 2.0, .NET 8+).

**Rough effort:** a walking skeleton (loader + config + tick hooks + commands + sample
plugin) is small; the full gameplay hook catalogue plus a curated world API is medium;
map *instances* are the one genuine architectural rework (medium–large) because map
identity is currently `int mapId` everywhere. Fully *procedural map geometry* is bounded
by the client, not the server: the stock EO client renders maps from its local `.emf`
files, so geometry mods require shipping patched client data (standard private-server
practice). Procedural *content* (spawns, items, chests, routes) is unrestricted and
server-side today.

---

## 1. Current state — the seams that already exist

| Seam | Where | Why it matters |
|------|-------|----------------|
| World tick | `src/Acorn/World/WorldHostedService.cs` — `PeriodicTimer`, `Task.WhenAll` over `WorldState.Maps` | One serialised loop; hooks here can't overlap ticks |
| Map tick | `src/Acorn/World/Map/MapState.cs` `Tick()` — delegates to `IMapController.Process*` | Per-map hook site; already the "moddable" unit |
| Packet dispatch | `src/Acorn/Net/PlayerState.cs` (~lines 299–358): resolve `IPacketHandler<TPacket>` from the per-connection `IEnumerable<IPacketHandler>`, check `[RequiresCharacter]`/`[RequiresState]`, invoke | A pre-dispatch filter can be inserted in exactly one place |
| Handler registration | `src/Acorn/Extensions/IocRegistrations.cs` `AddPacketHandlers()` / `AddAllOfType<T>()` — assembly scan into DI collections | Plugin assemblies can be scanned into the same collections |
| Chat commands | `TalkReportClientPacketHandler` dispatches `IEnumerable<ITalkHandler>` (admin `$`) and `IEnumerable<IPlayerCommandHandler>` | Plugins get player/admin commands for free via the existing dispatcher |
| Combat | `AttackUseClientPacketHandler` (player→NPC/PvP), `MapController.ProcessNpcActionsAsync` (NPC→player), `IFormulaService.CalculateDamageTo*` | Damage/kill hooks land in 2–3 call sites |
| Player lifecycle | `ConnectionHandler` (connect/disconnect ↔ `WorldState.TryAddPlayer/TryRemovePlayer`), `WelcomeRequestClientPacketHandler` (character enters world), `PlayerController.WarpAsync/DieAsync/EquipItemAsync` | Login/warp/death hooks are single-funnel points |
| Map registry | `WorldState._maps : ConcurrentDictionary<int, MapState>`, `MapForId(int)`, `TryReplaceMap` | The thing that must change for instancing |
| Dynamic NPCs | `SpawnNpcCommandHandler.SpawnNpc` — builds `NpcState` from an `EnfRecord`, `MapState.Npcs.TryAdd`, broadcasts `NpcAgreeServerPacket` | Proves runtime spawn/despawn works with the stock client |
| Config pattern | `src/Acorn/Options/*` with `SectionName` + `Configure<T>(configuration.GetSection(...))` in `Program.cs` | Plugin options follow the same pattern |

Two behaviours worth noting before designing:

1. **Handler resolution is first-match** — `_handlers.FirstOrDefault(h => handlerType.IsInstanceOfType(h))`,
   and DI `IEnumerable<T>` resolves in registration order. Whether plugins register
   before or after `AddPacketHandlers()` decides if they can *replace* core handlers.
   Recommendation: register core first (core wins), and expose modification through
   hooks/filters instead of replacement — predictable and parity-safe.
2. **Concurrency** — maps tick in parallel (`Task.WhenAll`), and packet handlers run on
   per-connection tasks concurrently with ticks. Plugin hooks invoked inline inherit
   this; plugin state must be thread-safe. This needs to be documented, not hidden.

---

## 2. Proposed architecture

```
┌────────────────────────────── Acorn (host) ──────────────────────────────┐
│  Program.cs                                                              │
│   ├─ discover plugins (plugins/<id>/plugin.json + config allow-list)       │
│   ├─ load each into its own AssemblyLoadContext (contracts resolve to      │
│   │   the default ALC → unified type identity)                            │
│   ├─ ConfigureServices: plugin modules + core registrations                │
│   └─ PluginHostedService: instantiate IAcornPlugin, OnLoadedAsync,         │
│       collect hook implementations into the dispatcher                     │
│                                                                           │
│  WorldHostedService ──► PluginHooks.OnWorldTick ──┐                       │
│  MapState.Tick ───────► PluginHooks.OnMapTick ────┤                       │
│  PlayerState dispatch ► PluginHooks.PacketFilter ─┼─► PluginHookDispatcher │
│  Attack/combat ───────► PluginHooks.OnDamage… ────┤   (order, try/catch,   │
│  ConnectionHandler ───► PluginHooks.OnLogin/Out ──┘    metrics, disable)   │
│                                                                           │
│  IWorldApi (curated facade over WorldState/controllers, injected to plugins)│
└────────────────────────────────────────────────────────────────────────────┘
        ▲ shared across the ALC boundary (default context):
        │   Acorn.Plugins (contracts), Moffat.EndlessOnline.SDK,
        │   Microsoft.Extensions.{DependencyInjection.Abstractions,Logging,…}
┌───────┴──────────────┐   ┌────────────────────────┐
│ plugins/acorn.pets/  │   │ plugins/acorn.dungeons/│  ← each: own ALC,
│   plugin.json        │   │   plugin.json          │    own private deps
│   Acorn.Plugin.Pets… │   │   …                    │
└──────────────────────┘   └────────────────────────┘
```

### 2.1 Contracts assembly — `src/Acorn.Plugins` (new project)

Small, stable, dependency-light (only the EO SDK for packet types +
`Microsoft.Extensions.*` abstractions). Published as a NuGet package so plugin authors
never reference the server. Everything that crosses the ALC boundary lives here —
never EF Core types, never `PlayerState`/`MapState` directly.

```csharp
public interface IAcornPlugin
{
    Task OnLoadedAsync(IPluginContext context, CancellationToken ct);
    Task OnUnloadingAsync(CancellationToken ct);   // graceful shutdown hook
}

public interface IPluginContext
{
    string PluginId { get; }
    IConfiguration Config { get; }        // bound to this plugin's config section
    ILogger Log { get; }                  // tagged with plugin.id
    IWorldApi World { get; }              // curated game API (below)
    IServiceProvider Services { get; }    // escape hatch: contracts-assembly interfaces only
}
```

### 2.2 Hooks — interface-based, notification vs intervention

Two flavours, both plain interfaces the plugin's exported classes implement (the
dispatcher collects them like DI collections do today):

- **Notification hooks** — observe only (`OnNpcKilled`, `OnPlayerLoggedIn`, …).
- **Intervention hooks** — mutable context object; plugin can veto or modify
  (`Cancel`, `Damage`, redirect target map, …). These are the parity-breaking ones and
  are documented as such.

```csharp
public interface IPluginHook
{
    int Priority => 0;   // lower runs first
}

public interface IWorldTickHook : IPluginHook
{
    Task OnWorldTickAsync(WorldTickContext ctx);
}

public sealed class DamageToNpcContext
{
    public required IPlayerView Attacker { get; init; }
    public required INpcView Target { get; init; }
    public int Damage { get; set; }          // intervention: modify
    public bool Cancel { get; set; }         // intervention: veto the hit
}

public interface IDamageToNpcHook : IPluginHook
{
    Task OnDamageToNpcAsync(DamageToNpcContext ctx);
}
```

**Dispatch semantics** (in `PluginHookDispatcher`, a singleton):

- Hooks are awaited inline at the call site (tick stays serialised; combat stays in the
  packet's own task).
- Each plugin invocation wrapped in `try/catch` — one broken plugin cannot kill the
  world tick or a connection.
- Failures counted per plugin; after `Plugins:FailureThreshold` consecutive errors the
  plugin is auto-disabled for the run and logged critical (quarantine).
- Metrics via the existing `AcornMetrics` meter: `acorn.plugin.hook.duration`,
  `acorn.plugin.hook.errors` tagged with `plugin.id`.
- No enforced timeout in v1 (can't safely abort arbitrary code); documented guidance:
  hooks must be fast, queue long work yourself.

### 2.3 World API — `IWorldApi`

The power surface, wrapping live state in thin facades (views), not copies, so
mutations are real but internals stay hidden:

- **Queries:** online players, players on map, map info, character views (stats,
  inventory/paperdoll reads), NPC views.
- **Actions:** send packet to player / broadcast to map (SDK `IPacket`), server
  announcement (`INotificationService`), warp player, spawn/despawn NPC (the
  `SpawnNpcCommandHandler` flow, productised), add/remove ground items
  (`IMapItemService`), damage/heal (via the same funnels combat uses).
- **Data:** pub file lookups (EIF/ENF/ESF/ECF) — read-only.
- **Commands:** register `IPlayerCommandHandler`/`ITalkHandler` implementations into
  the existing dispatchers.

### 2.4 Loader & isolation

Recommendation: **plain `AssemblyLoadContext` + `AssemblyDependencyResolver`** (the
MS tutorial pattern, ~150 lines, zero third-party deps, full control on .NET 11
previews). `DotNetCorePlugins` 2.0 is a fine accelerator if we later want its hot-reload
file-watcher scaffolding, but it adds a dependency for little Phase 0 gain.

Key rules (all standard):

- Contracts assembly, EO SDK and `Microsoft.Extensions.*` abstractions are **shared**:
  plugin ALC `Load()` returns `null` for them so they unify with the host's copies.
  Plugin csproj references them with `<Private>false</Private>` and sets
  `<EnableDynamicLoading>true</EnableDynamicLoading>`.
- Everything else is private to the plugin's ALC → conflicting NuGet versions between
  plugins (and vs. the host) are fine.
- Plugins target `net11.0` (not netstandard), so `.deps.json` probing works.
- The loader validates that manifest entry paths stay inside the configured plugins
  directory (no path traversal / arbitrary code execution via config).

### 2.5 Packaging, discovery & config

```
plugins/
  acorn.pets/
    plugin.json                  # manifest
    Acorn.Plugin.Pets.dll        # entry assembly
    Acorn.Plugin.Pets.deps.json
    <private deps>.dll
```

```jsonc
// plugin.json
{
  "id": "acorn.pets",
  "name": "Pets",
  "version": "1.2.0",
  "entryAssembly": "Acorn.Plugin.Pets.dll",
  "contractVersion": 1,          // host rejects incompatible contracts
  "minHostVersion": "0.1.0"
}
```

```jsonc
// appsettings.json
{
  "Plugins": {
    "Enabled": true,
    "Directory": "plugins",
    "Load": [ "acorn.pets" ],           // explicit allow-list; discovery alone never activates
    "FailureThreshold": 50
  },
  "PluginOptions": {
    "acorn.pets": { "MaxPetsPerPlayer": 2 }   // bound to IPluginContext.Config
  }
}
```

Distribution = zip the plugin folder (or `dotnet publish` output); server owners drop it
in `plugins/` and add the id to `Load`. Later: NuGet for contracts + a template repo,
and optionally a community registry. Loaded plugins are logged at startup and (Phase 1+)
exposed via `Acorn.Api`.

### 2.6 Lifecycle & startup order

Loading happens **before** `Host.CreateDefaultBuilder` finishes configuring services, so
plugins can participate in DI (`ConfigureServices` via an optional module interface,
command handlers, hosted services). Order in `Program.cs`:

1. Build configuration → discover + load plugin assemblies (fail fast on bad manifests).
2. `ConfigureServices`: core registrations first, then plugin registrations (core wins
   first-match handler resolution).
3. `PluginHostedService` (registered before `WorldHostedService`) instantiates
   `IAcornPlugin`s, collects hooks, raises `OnLoadedAsync` before the first tick.
4. Shutdown: `OnUnloadingAsync` after `ShutdownPersistenceHostedService` has persisted
   online characters (register accordingly — hosted services stop in reverse order).

**Phase 0 note:** discovery runs before the host builds (fail-fast, as above), but
plugin participation in `ConfigureServices` is not implemented yet — plugins are
instantiated by `ActivatorUtilities` against the host container and talk to the world
only through `IPluginContext`. A module/DI interface remains a later-phase item.

**Hot reload / unloading is explicitly out of scope initially.** Collectible ALCs need
disciplined rooting (DI container, statics, event subs, `_handlerInvokeCache`-style
caches would all pin the assembly), and the deploy story here is containerised with
restarts anyway. Startup-loaded + restart-to-apply is the honest v1; revisit later.

---

## 3. Hook catalogue (Phase 1 target)

| Hook | Host call site | Kind | Example mods |
|------|----------------|------|--------------|
| `OnWorldTick` | `WorldHostedService` loop | notify | scheduled events, world bosses |
| `OnMapTick(map)` | `MapState.Tick` | notify | hazards, zone effects, dungeon timers |
| `OnPacket(filter)` | `PlayerState` dispatch, pre-handler | intervene (cancel) | anti-cheat, rate tweaks, feature gating |
| `OnPlayerAttack` | `AttackUseClientPacketHandler.HandleAsync` (early) | intervene (cancel) | cooldown mods, PvP zones, duel systems |
| `OnDamageToNpc` | attack handler, before `target.Hp` mutation | intervene (damage, cancel) | elemental mods, pet damage attribution |
| `OnDamageToPlayer` | PvP path + `MapController.ProcessNpcActionsAsync` NPC attacks | intervene | difficulty mods, protection auras |
| `OnNpcKilled` | attack handler death branch (near `_questService.NotifyNpcKilled`) | notify | kill quests, pet XP, boss mechanics |
| `OnPlayerDied` | `PlayerController.DieAsync` | notify/intervene | death penalties, respawn overrides |
| `OnPlayerConnected/Disconnected` | `ConnectionHandler` ↔ `TryAddPlayer/TryRemovePlayer` | notify | session analytics, login rewards |
| `OnCharacterEnteredWorld` | `WelcomeRequestClientPacketHandler` | notify/intervene | instance assignment, first-login logic |
| `OnMapEnter/OnMapLeave` | `PlayerController.WarpAsync` / `MapController.WarpPlayerAsync` | notify + intervene (redirect mapId) | **dungeon instance routing** |
| `OnItemPickup/Drop/Use/Equip` | Item packet handlers, `EquipItemAsync` | notify/intervene | curse items, soulbound, pet feeding |
| `OnSpellCast` | `ISpellCastService` | notify/intervene | custom spell effects |
| `OnTalkCommand` (register commands) | existing `ICommandHandler` dispatchers | extend | all of the above, surfaced as `@pet`, `@dungeon` |

---

## 4. Feasibility of the motivating scenarios

### 4.1 Pets — **easy** (Phase 1 surface is sufficient)

All primitives exist: runtime NPC spawn (proven by `SpawnNpcCommandHandler`), map tick
for follow/defend AI (`OnMapTick` + NPC views + `NpcState` movement), combat attribution
(`OnDamageToNpc`/`OnPlayerAttack` hooks), despawn, and per-player state (plugin tracks
owner→pet-NPC-index in memory; persistence via the Phase 3 storage API or plugin-owned
JSON). Constraints: the pet must use an NPC id present in the client's ENF (appearance
comes from client data), and the 255-NPC-per-map protocol cap applies. Command surface
(`@pet follow/stay`) comes free via registered `IPlayerCommandHandler`s.

### 4.2 Procedural dungeons — **two halves, very different difficulty**

- **Content generation (easy):** randomised NPC spawns, loot, chest contents, door
  states, routes and objectives on *existing* map ids — all server-side state the stock
  client renders normally. `OnMapEnter` + world API is enough.
- **Geometry generation (bounded by the client):** the server never transmits map
  geometry; the client renders from its local `.emf`. Options, in order of practicality:
  1. **Reuse existing layouts**, vary content (works with stock clients today).
  2. **Ship patched client data with the mod**: the EO SDK can read/write `Emf`, so a
     dungeon plugin can bundle generated `.emf` files and the server operator
     distributes them as a client patch — normal private-server practice, but the mod
     becomes "server plugin + client data pack".
  3. **Custom clients** (e.g. the eoweb client served via Caddy) could accept
     server-authored geometry — needs investigation of what eoweb supports (open
     question).

### 4.3 Map instances — **medium rework, well-contained** (Phase 2)

The blocker is identity, not state: `MapState` instances are already fully independent
objects (own NPCs, items, doors, chests, tick counters) sharing only immutable `Emf`
data — two `MapState`s over the same map id would already "work". What's coupled to
`int mapId`:

- `WorldState._maps` key + `MapForId(int)` (single funnel — good).
- `Character.Map` (persisted), warp destinations resolved by id, `IMapCacheService`,
  API summaries, admin commands.

Proposed incremental design:

1. Introduce `IMapRegistry` wrapping `WorldState._maps`: `GetMap(int mapId)` returns the
   **default instance**; add `CreateInstance(int mapId) → MapState`,
   `DestroyInstance(handle)`, `InstancesOf(int mapId)`.
2. Add a **map-resolution step** to the warp funnel(s): `(player, requestedMapId) →
   MapState`, with an intervention hook (`OnMapEnter` redirect) so a dungeon plugin can
   assign/refresh instances. Default behaviour = today's semantics.
3. Persistence rule: store the logical map id; on login players resolve to the default
   instance unless a plugin claims them.
4. Instance lifecycle: destroy (or idle out) instances when empty; plugin decides.

This keeps the client oblivious (same map id, same data) and touches a bounded set of
call sites. It's the one item that deserves its own design pass before building.

### 4.4 Core vs. plugin governance

EOSERV-parity logic stays in core; hooks are **additive by default**. Intervention hooks
are the contract-stability and parity risk, so: keep them few and coarse (cancel/damage
numbers/redirect), document each as parity-affecting, and log/expose which plugins are
loaded (operators and players can see a server is modded). Anything two servers would
implement identically (e.g. a widely used pet system) can graduate into core later —
the hook sites make that migration mechanical.

---

## 5. Plugin author experience (target)

```xml
<!-- Acorn.Plugin.Pets.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Acorn.Plugins" Version="1.0.0">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

```csharp
public sealed class PetPlugin : IAcornPlugin, IPlayerCommandHandler, IMapTickHook
{
    private IPluginContext _ctx = null!;

    public Task OnLoadedAsync(IPluginContext context, CancellationToken ct)
    {
        _ctx = context;
        return Task.CompletedTask;
    }

    public IEnumerable<string> Commands => ["pet"];

    public async Task HandleAsync(IPlayerView player, string command, string args)
    {
        if (args == "summon")
            await _ctx.World.SpawnNpcAsync(player.MapId, npcId: 123, player.X, player.Y);
    }

    public Task OnMapTickAsync(MapTickContext ctx)
    {
        // move each owner's pet one step towards its owner, broadcast, etc.
        return Task.CompletedTask;
    }

    public Task OnUnloadingAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## 6. Alternatives considered

| Option | Verdict |
|--------|---------|
| **In-process ALC plugins (proposed)** | Best fit: full-speed tick hooks, DI-native, type-safe, matches existing patterns. Trust boundary: plugins are trusted code (server owner installs them) — ALC isolates *dependencies*, not *malice*. |
| Scripting mods (Lua via MoonSharp/NLua, JS via Jint, C# via Roslyn) | Lower barrier for non-.NET modders, safer sandbox, but per-tick interpreted overhead and a parallel API surface. Reasonable as a *later layer on top of the same hooks* — not a replacement. |
| Out-of-process plugins (gRPC/queue) | Real isolation & crash safety, but latency in tick/combat paths and heavy serialization of world state. Wrong trade-off for a 100ms game loop; could suit analytics-style sidecars. |
| Compile-time "fork modules" (status quo) | Free, but no packaging/sharing story — the thing we actually want. |

---

## 7. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Contract churn breaks published plugins | SemVer `Acorn.Plugins`; `contractVersion` check at load; public-API analyzer on the contracts project; additive-only policy between majors |
| Type identity errors across ALC (the classic `InvalidCastException`) | Shared-type list enforced by loader; contract/SDK/MS.Extensions never copied into plugin output (`Private=false`); contract tests that load a sample plugin from disk in CI |
| Slow/misbehaving plugin stalls ticks | Inline hooks documented as latency-critical; per-plugin duration metrics; error counting + auto-disable threshold |
| Concurrency bugs in plugin state | Hooks may run concurrently across maps/connections — documented loudly; sample plugin demonstrates safe patterns |
| Unloading leaks (if hot reload ever added) | Deferred; when attempted: child provider per plugin, WeakReference probes, drain-then-swap (as per .NET guidance) |
| Malicious plugin | Not solvable in-process — explicit trust model: operators install only plugins they trust; manifest path validation; startup log + API listing of loaded plugins; optional signing later |
| First-match handler override accidents | Core registers before plugins; replacement is not the extension mechanism; filters/hooks are |

---

## 8. Roadmap

| Phase | Scope | Size |
|-------|-------|------|
| **0 — Walking skeleton** _(implemented)_ | `src/Acorn.Plugins` contracts project; `PluginLoadContext` + discovery/manifest/config (`Plugins` section); `PluginHostedService` lifecycle; `OnWorldTick`/`OnMapTick`; command registration bridge; `HelloAcorn` sample plugin under `samples/`; contract tests; docs | S–M |
| **1 — Gameplay hooks + world API** | Combat hooks (attack/damage/kill/death), lifecycle hooks (connect/login/enter-world/warp), item + spell hooks, pre-dispatch packet filter; `IWorldApi` (spawn/despawn, ground items, broadcast, warp, announce, pub data); error isolation + metrics + auto-disable; loaded-plugins endpoint in `Acorn.Api`; **pets sample plugin** | M |
| **2 — Instances & dungeons** | `IMapRegistry` (logical id vs instance), warp resolution funnel + redirect hook, instance lifecycle, persistence rules; **dungeon sample plugin** (instanced encounters on existing layouts) | L |
| **3 — Ecosystem** | Per-plugin per-player persisted storage API; EMF generation/writing helpers + client-data-pack tooling; hot reload investigation; plugin dependencies & signing; NuGet publish + template repo | M–L each |

### 8.1 Phase 0 — implementation notes (shipped)

- **Contracts** live in `src/Acorn.Plugins` (standalone project;
  `Microsoft.Extensions.*` abstractions come from the shared framework on .NET 11).
  Plugins reference it with `<Private>false</Private>` +
  `<EnableDynamicLoading>true</EnableDynamicLoading>` so the host's copy is never
  duplicated into plugin output.
- **Discovery** (`PluginDiscovery`): only ids listed in `Plugins:Load` are activated.
  Missing/invalid manifest, id mismatch, contract mismatch, missing entry assembly
  or an entry path that escapes the plugin folder is a fatal `PluginLoadException`
  (fail fast at startup). Requested ids are de-duplicated case-insensitively.
- **Loading** (`PluginLoadContext`): one non-collectible ALC per plugin;
  `Acorn.Plugins`, `Moffat.EndlessOnline.SDK` and `Microsoft.Extensions.*` return
  `null` from `Load()` and unify with the host's copies (contract type identity
  across the boundary). `.deps.json` is optional for dependency-free plugins.
- **Lifecycle** (`PluginHostedService`): registered before the listeners and world
  tick so `OnLoadedAsync` completes before the first connection can observe hooks;
  `OnUnloadingAsync` runs in reverse order at shutdown. A plugin that throws during
  instantiation or `OnLoadedAsync` is disabled (logged critical) rather than taking
  the host down — only discovery failures are fatal.
- **Hooks** (`PluginHookDispatcher`): collected from the single `IAcornPlugin`
  entry instance, ordered by `Priority` (lower runs first), each invocation
  try/caught so one broken plugin cannot kill a tick; failures count per plugin and
  `Plugins:FailureThreshold` consecutive failures auto-disable the plugin for the
  run. Hook duration/error metrics are Phase 1.
- **Commands**: `PluginCommandAdapter` bridges `IPluginCommand` into the existing
  `IEnumerable<IPlayerCommandHandler>` dispatch (`#command`). Two registration steps
  carry ordering rules: `AddPlugins(catalog)` early (so `PluginHostedService` starts
  before the listeners) and `AddPluginCommands(catalog)` **after**
  `AddAllOfType<IPlayerCommandHandler>()` (so built-in commands match first).
  The adapter is marked `[SkipAutoRegistration]` because the convention scan cannot
  construct its `PluginEntry` parameter — only the factory registration can.
- **Config**: the `Plugins` section (enabled / directory / load / failure
  threshold); per-plugin options under `PluginOptions:<id>` surface as
  `IPluginContext.Config`.
- **Sample**: `samples/HelloAcorn` implements `IAcornPlugin`, both tick hooks and a
  `#hello` command, with a Debug-level heartbeat every 10 world ticks. Publish it to
  `plugins/hello-acorn/` and add `hello-acorn` to `Plugins:Load` to activate it.
- **Tests**: `tests/Acorn.Tests/Plugins/` — discovery/validation paths, dispatch
  semantics (priority order, error isolation, auto-disable, failure reset) and an
  end-to-end test that loads the built sample from disk into its own ALC, asserting
  the load-context split *and* that `IAcornPlugin` casts succeed (type identity).

## 9. Open questions

1. Do we want a scripting layer (Lua/JS) for non-C# modders on top of the same hooks?
2. What can the eoweb client do — could it render server-authored (procedural) maps or
   custom pet visuals, sidestepping the client-data constraint?
3. Plugin→plugin dependencies/interoperability (Reloaded-style shared interfaces) — v2?
4. Plugin-owned player data: dedicated DB table (migration) vs JSON files vs cache-only?
5. Should loaded plugins be surfaced to players (e.g. in `WelcomeMsg`/news) or operators
   only?
6. Contract versioning cadence — tie `Acorn.Plugins` major to server major?
