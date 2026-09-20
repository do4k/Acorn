# Commands

Acorn's in-game commands are handled by `TalkReportClientPacketHandler`
(`src/Acorn/Net/PacketHandlers/Player/Talk/TalkReportClientPacketHandler.cs`).
It recognises two prefixes:

| Prefix | Scope | Who can use it |
|--------|-------|----------------|
| `$`    | Admin commands | Any character with `Admin > Player` (Spy or above) |
| `#`    | Player commands | Every player |

Commands are case-insensitive and arguments are whitespace separated. Unrecognised
`#` commands fall through to normal local chat; unrecognised `$` commands are ignored.

Each command lives in its own `*CommandHandler.cs` file and implements either
`ITalkHandler` (admin) or `IPlayerCommandHandler` (player). Handlers are discovered
automatically by convention via `AddAllOfType<T>()` — no manual DI registration is
required.

## Admin commands (`$`)

The `$` prefix already limits commands to non-player admin accounts. Individual
handlers may enforce a higher minimum level through `IAdminService`; the
**Minimum level** column below is the effective requirement.

| Command | Aliases | Minimum level | Description |
|---------|---------|---------------|-------------|
| `$warp` | `$w` | Spy | Warp to a map by id or name, optionally `[<x> <y>]`. Defaults to map centre. |
| `$wmt` | `$warpmeto` | LightGuide | Warp to another online player's location. |
| `$summon` | `$bring`, `$warptome` | Guardian | Warp another online player to your location. |
| `$who` | `$online` | Spy | List online players with their map and coordinates. |
| `$info` | `$player` | LightGuide | Show a target player's stats and location. |
| `$inventory` | `$inv` | GameMaster | Show another player's inventory and bank. |
| `$kick` | | Guardian | Disconnect a player. |
| `$jail` | | GameMaster | Warp a player to the configured jail map. |
| `$free` | | GameMaster | Release a player from jail (warp home). |
| `$ban` | | GameMaster | Ban a player's account/HDID and disconnect them. |
| `$mute` | | GameMaster | Temporarily mute a player's chat. |
| `$unmute` | | GameMaster | Remove a player's mute. |
| `$freeze` | | Guardian | Freeze a player's movement. |
| `$unfreeze` | | Guardian | Restore a player's movement. |
| `$hide` | `$show` | Guardian | Toggle your visibility to other players. |
| `$evacuate` | | GameMaster | Warp everyone on your map to the home map. |
| `$quake` | | GameMaster | Trigger a screen quake, optional strength `1`–`8`. |
| `$global` | | GameMaster | Send a server-wide announcement. |
| `$set` | | Spy | Set a player attribute: `$set <player> <attribute> <value>`. |
| `$spawnitem` | `$sitem`, `$si` | Spy | Add item(s) to your inventory by id or name, optional amount. |
| `$spawnnpc` | `$snpc` | Spy | Spawn NPC(s) at your position by id or name, optional count. |
| `$addspell` | `$spell` | Spy | Learn a spell for a named character, optional level. |
| `$repub` | | Spy | Re-read the pub data files (`ECF`/`EIF`/`ENF`/`ESF`) and refresh the pub cache. |
| `$rehash` | | Spy | Re-read the server configuration sources. Bound settings still need a restart. |
| `$uptime` | | Spy | Show how long the server has been running. |

## Player commands (`#`)

| Command | Aliases | Description |
|---------|---------|-------------|
| `#loc` | `#location` | Show your current map id/name and coordinates. |
| `#inventory` | `#inv` | Show how many items you are carrying. |
| `#usage` | | Show your total play time. |

## Adding a command

1. Create `<Name>CommandHandler.cs` in `src/Acorn/Net/PacketHandlers/Player/Talk/`
   (one type per file — see `.editorconfig` / `SA1402`).
2. Implement `ITalkHandler` (admin `$`) or `IPlayerCommandHandler` (player `#`).
3. Match the command name(s) case-insensitively in `CanHandle`.
4. Keep game logic in a service and call it from the handler where practical.
5. Add unit tests under `tests/Acorn.Tests/`.
