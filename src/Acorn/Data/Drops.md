# NPC Drop Tables

`drops.json` holds the per-NPC loot tables and `global_drops.json` the drops
that are rolled on every NPC kill in addition to them. Both are loaded once at
startup by `DropTableHostedService` via `DropFileLoader`, then `LootService`
is sealed.

These replaced the old `drops.txt` / `global_drops.txt` pair. The percentages
are unchanged, including the fractional ones.

## `drops.json`

```json
{
  "npc_drops": [
    {
      "npc_id": 9,                       // ENF NPC index (1-indexed), see below
      "drops": [
        { "item_id": 1,  "min_amount": 100, "max_amount": 200, "rate_percent": 5 },
        { "item_id": 22, "min_amount": 1,   "max_amount": 1,   "rate_percent": 2 }
      ]
    }
  ]
}
```

## `global_drops.json`

```json
{
  "global_drops": [
    { "item_id": 1, "min_amount": 1, "max_amount": 5, "rate_percent": 15 }
  ]
}
```

## Notes

- **`npc_id` is the ENF NPC index** (1-indexed), not the ENF `behavior_id`.
  It is the same id space as the shop and skill master data and matches
  `NpcState.Id`, which is what `LootService.RollDrop(npcId)` is called with.
  Almost every monster has `behavior_id` 0, so keying on that would collapse
  the whole table into one entry.
- **`rate_percent` is a percentage (0-100), not a fraction.** `10` means 10%.
  Fractional values are valid and preserved as written - `0.5` means half a
  percent. A previous conversion stored `chance / 100 * 1000` in a field that
  was then read as a percentage, which turned every chance of 10% or more into a
  guaranteed drop; `DropFileLoaderTests` guards against that regressing.
- **How a drop is chosen** follows eoserv's `DropRateMode 3`: the NPC's own
  entries and the global entries go into one weighted roll. Below 100 total the
  remainder is "no drop"; above 100 the entries are scaled down proportionally
  and a drop is guaranteed.
- `DropFileLoader` rejects an entry with a non-positive `item_id`, an inverted
  `min_amount`/`max_amount` pair, or a rate outside 0-100, and logs it, keeping
  the rest of the table. A repeated `npc_id` keeps the first table and warns.
  A malformed or missing file logs an error and loads nothing rather than
  throwing, so one bad edit cannot stop the server from booting.
