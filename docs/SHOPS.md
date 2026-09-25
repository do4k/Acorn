# Shops

Shop NPCs (`NpcType.Shop` in the ENF) are matched to their inventory by the
NPC's `behavior_id` (the ENF vendor id). In this data set, shop vendors use
behavior ids 1-41. Each shop is one JSON file in `Data/Shops/`.

Like the pub files and maps, `Data/Shops/` is **per-deployment content** and is
git-ignored. The compose stack bind-mounts `./src/Acorn/Data` read-only into
the server containers, so shop files placed there on the host are live after
the next `docker compose up -d` (a container recreate, no image rebuild).

When the directory is missing or empty at startup, `ShopDataRepository`
generates a placeholder `sample_shop.json`.

## File format

```json
{
  "behavior_id": 19,          // ENF behavior/vendor id of the shop NPC
  "name": "Centaur Weapon Smith", // shown in the client shop window
  "min_level": 0,             // 0 = no minimum (enforced on Shop/Open)
  "max_level": 0,             // 0 = no maximum
  "class_requirement": 0,     // 0 = any class
  "trades": [
    {
      "item_id": 24,          // EIF item id
      "buy_price": 1700,      // gold the player pays (0 = not sold by this shop)
      "sell_price": 170,      // gold the shop pays (0 = not bought by this shop)
      "max_amount": 99        // per-transaction cap for buying and selling
    }
  ],
  "crafts": [
    {
      "item_id": 359,         // crafted result
      "ingredients": [        // max 4; extra entries are truncated at load
        { "item_id": 357, "amount": 1 },
        { "item_id": 358, "amount": 1 },
        { "item_id": 326, "amount": 1 },
        { "item_id": 353, "amount": 1 }
      ]
    }
  ]
}
```

`buy_price` / `sell_price` are named from the player's point of view: the
`buy_price` is what the player hands over, the `sell_price` is what the shop
pays back. A `0` in either field means "this shop does not deal in that
direction" - `ShopBuyClientPacketHandler` requires `buy_price > 0` and
`ShopSellClientPacketHandler` requires `sell_price > 0`, so a `0`ed row only
shows up in the shop window as a non-buyable / non-sellable entry. That is
deliberate and used heavily by the source data: the Witch, Marble and Dragon
Warrior all buy items without ever selling them.

## Validation

`ShopDataRepository` logs warnings at startup for: unknown item ids, negative
prices, `sell_price > buy_price` (an infinite-money exploit), duplicate
behavior ids (first file wins) and crafts with more than four ingredients.
`ShopDataRepositoryTests` additionally validates the real data files against
the real EIF when the per-deployment data is present.

## Interaction range

Both the native and web clients send `Shop/Open` (and the bank/barber/trainer
equivalents) the moment the NPC sprite is clicked - they do not walk to the
vendor first. `NpcInteractionHelper` therefore validates NPC interactions
against the player's client view (the same 11/14-tile asymmetric Manhattan
cull range that decides which entities are sent to the client), not a fixed
adjacency radius: anything a real player can see and click works, while
crafted packets naming NPCs outside the player's view or the wrong NPC type
are rejected and logged with both positions. Buying, selling and crafting
additionally require the interaction to have started with `Shop/Open` and
survive until the next step, since walking clears it.

## Current content

The shop set in this deployment covers all 41 ENF vendors (general stores,
tavern, jeweller, shoe makers, weapon/armor shops, event shops, ...). The
inventories, prices and craft recipes come from the original Endless Online
shop list (ripped and converted by Rena & Ducci), which is keyed by ENF NPC
index rather than by `behavior_id`. The two line up one-to-one for the first 40
vendors - the n-th shop NPC in the ENF carries `behavior_id` n, and the
reference list's n-th entry is that same NPC - so the mapping is purely
positional. Behaviour id 41 (ENF NPC 262, Ben) has no entry in the reference
list, so `bens_trading_post.json` keeps its hand-authored inventory.

Conventions in the current data:

- `buy_price` and `sell_price` are transcribed verbatim from the source, which
  includes rows where either side is `0` (see above);
- `max_amount` is `99` everywhere: the source has no per-transaction cap, and
  the weight limit plus the gold check in the handlers are the real limits;
- `Dragon Warrior` (vendor 40) requires level 10 to open. The source carries no
  level data; this was kept from the previous data set so the enforced
  level/class requirements stay exercised.
- One known source quirk is preserved verbatim: `Aeven Grocery` (vendor 3)
  lists item 6 (Love Letter) twice, at `120/10` and `120/8`. Buying and selling
  resolve trades with `FirstOrDefault`, so the first row wins and the second is
  unreachable.
