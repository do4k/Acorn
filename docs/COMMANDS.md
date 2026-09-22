# Commands

Acorn's in-game commands are handled by `TalkReportClientPacketHandler`
(`src/Acorn/Net/PacketHandlers/Player/Talk/TalkReportClientPacketHandler.cs`).
It recognises two prefixes:

| Prefix | Scope | Who can use it |
|--------|-------|----------------|
| `$`    | Admin commands | Character `Admin >= Player` (Spy or above) |
| `#`    | Player commands | Every player |

Commands are case-insensitive and arguments are whitespace separated. Unrecognised
`#` commands fall through to normal local chat; unrecognised `$` commands are ignored.

Every handler implements `ICommandHandler` (or the admin/player specialisations)
and declares:

- `Commands` — the command name(s) it answers, without the prefix.
- `Usage` — the argument usage shown by help (optional).
- `RequiredLevel` (admin handlers only) — the minimum `AdminLevel`, enforced by
  the dispatcher at `src/Acorn/Net/PacketHandlers/Player/Talk/TalkReportClientPacketHandler.cs`.

Handlers are discovered automatically by convention via `AddAllOfType<T>()` — no
manual DI registration is required.

## Admin commands (`$`)

Run `$help` in game for the list of commands your admin level can use.

| Command | Aliases | Minimum level | Description |
|---------|---------|---------------|-------------|
| `$help` | | Spy | List available commands, or show one command's usage. |
| `$warp` | `$w` | Spy | Warp to a map by id or name, optionally `[<x> <y>]`. Defaults to map centre. |
| `$wmt` | `$warpmeto` | LightGuide | Warp to another online player's location. |
| `$summon` | `$bring`, `$warptome` | Guardian | Warp another online player to your location. |
| `$who` | `$online` | Spy | List online players with their map and coordinates. |
| `$info` | `$player` | LightGuide | Show a target player's stats and location. |
| `$inventory` | `$inv` | GameMaster | Show another player's inventory and bank. |
| `$kick` | `$skick` (silent) | Guardian | Disconnect a player. |
| `$jail` | `$sjail` (silent) | GameMaster | Warp a player to the configured jail map. Persists across relog. |
| `$free` | | GameMaster | Release a player from jail (warp home). |
| `$ban` | `$sban` (silent) | GameMaster | Ban a player's account/HDID and disconnect them. Persists to the database. |
| `$mute` | `$smute` (silent) | GameMaster | Temporarily mute a player's chat. |
| `$unmute` | | GameMaster | Remove a player's mute. |
| `$freeze` | | Guardian | Freeze a player's movement. Persists across relog. |
| `$unfreeze` | | Guardian | Restore a player's movement. |
| `$hide` | `$show` | Guardian | Toggle your visibility to other players. |
| `$evacuate` | | GameMaster | Warp everyone on your map to the home map. |
| `$quake` | | GameMaster | Trigger a screen quake, optional strength `1`–`8`. |
| `$global` | | GameMaster | Send a server-wide announcement. |
| `$set` | | GameMaster | Set a player attribute: `$set <player> <attribute> <value>`. The `admin` attribute additionally requires HighGameMaster. |
| `$spawnitem` | `$sitem`, `$si` | Guardian | Add item(s) to your inventory by id or name, optional amount. |
| `$spawnnpc` | `$snpc` | Guardian | Spawn NPC(s) at your position by id or name, optional count. |
| `$addspell` | `$spell` | Guardian | Learn a spell for a named character, optional level. |
| `$item` | | Spy | Look up item data by id or name. |
| `$npc` | | Spy | Look up NPC data by id or name. |
| `$spellinfo` | | Spy | Look up spell data by id or name. |
| `$class` | | Spy | Look up class data by id or name. |
| `$qstate` | | GameMaster | Show a character's quest progress. |
| `$guild` | | GameMaster | Create a guild with yourself as leader: `$guild create <tag> <name> [-- <description>]`. Tags are 2–3 uppercase letters; names keep the casing you type. Bypasses the gold cost and NPC requirement. |
| `$repub` | | Spy | Re-read the pub data files (`ECF`/`EIF`/`ENF`/`ESF`) and refresh the pub cache. |
| `$rehash` | | Spy | Re-read configuration sources and refresh pub files. Settings bound at startup still need a restart. |
| `$remap` | | GameMaster | Re-read a single map file (`<id>.emf`) from disk and swap it into the world. The map must be empty of players; run again after they leave. |
| `$shutdown` | | HighGameMaster | Broadcast a shutdown notice (optional reason) and stop the host gracefully. All online characters are persisted first. |
| `$undress` | | GameMaster | Force-unequip everything a named online player is wearing into their inventory. Cursed items resist removal. |
| `$uptime` | | Spy | Show how long the server has been running. |

### `$set` attributes

`sitstate` takes an enum name (`Stand`, `Sit`, …); `hidden` and `nointeract`
accept `true`/`false` or `1`/`0`. All other attributes take an integer.

## Player commands (`#`)

Run `#help` in game for this list.

| Command | Aliases | Description |
|---------|---------|-------------|
| `#help` | | List player commands, or show one command's usage. |
| `#loc` | `#location` | Show your current map id/name and coordinates. |
| `#inventory` | `#inv` | Show how many items you are carrying. |
| `#usage` | | Show your total play time. |

## Acornbot (whisper commands)

Acornbot is a built-in bot that players reach by whispering it, e.g.
`!acornbot title Cool Dude` in local chat (the client sends that as a whisper)
or by whispering `title Cool Dude` to a character named `Acornbot`. It exposes a
curated set of self-service commands to everyone - like admin commands, but only
the ones a player may run. Acornbot answers back in the whisper channel.
Send just `help` (or an empty whisper) for the command list.

| Command | Description |
|---------|-------------|
| `title <text>` | Set the title shown under your character name. `title clear` removes it. |
| `whoami` (`me`) | Read-only summary of your own character: identity, location, vitals, gold. |
| `help` | List the commands this bot accepts. |

Every player can use the bot regardless of admin level - an admin is implicitly a
player here; there is no `RequiredLevel` on bot commands. While the bot is
configured, character creation refuses names matching the bot handle
(case-insensitively) so the whisper handle can never be shadowed.

Title changes are persisted and the player is re-announced to nearby viewers via
`Msg_Players`/Agree (no re-warp animation). Note: the map `Player` struct in this
protocol revision carries no title field, so viewers see the new title via the
book/paperdoll windows and the acting player's own client picks it up on
re-login - a protocol/client limitation, not a server one.

Configure via the `Acornbot` section of `appsettings.json`:

```json
"Acornbot": {
  "Enabled": true,
  "Name": "Acornbot",
  "Title": { "MaxLength": 32, "CostItemId": 1, "CostAmount": 0 }
}
```

- `Enabled` turns the whole bot on/off; while off, whispers to the name fall
  through to normal player lookup.
- `Title` options are the cost model: `CostItemId` is the inventory id consumed
  per `title` use - `1` is gold - with `CostAmount` the quantity. The default
  (`CostAmount: 0`) makes titles free. Servers using a "title certificate" item
  set that item's id instead. Clearing a title is always free.

New bot commands implement `IAcornbotCommand` under
`src/Acorn/Net/PacketHandlers/Player/Talk/Acornbot/` and are discovered by
`AddAllOfType<IAcornbotCommand>()`.

## Banned symbols (global text policy)

`IBannedTextPolicy` (section `BannedText` in `appsettings.json`) is a single
global deny list of case-insensitive substrings applied to all user-supplied
naming text: character names at creation, guild tags/names/descriptions and
Acornbot titles.

```json
"BannedText": { "Symbols": ["#", "$", "'", "~"] }
```

An empty list (the code default) disables the check. Entries may be multi-character.

## Adding a command

1. Create `<Name>CommandHandler.cs` in `src/Acorn/Net/PacketHandlers/Player/Talk/`
   (one type per file — see `.editorconfig` / `SA1402`).
2. Implement `ITalkHandler` (admin `$`) or `IPlayerCommandHandler` (player `#`).
3. Declare `Commands`, and optionally `Usage` / `RequiredLevel`.
4. Keep game logic in a service and call it from the handler where practical.
5. Add unit tests under `tests/Acorn.Tests/`. Alias uniqueness and DI resolution
   are guarded by `CommandRegistrationTests`.
