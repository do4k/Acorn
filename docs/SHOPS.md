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
  "name": "Aeven Forge",      // shown in the client shop window
  "min_level": 0,             // 0 = no minimum (enforced on Shop/Open)
  "max_level": 0,             // 0 = no maximum
  "class_requirement": 0,     // 0 = any class
  "trades": [
    {
      "item_id": 19,          // EIF item id
      "buy_price": 250,       // gold the player pays (0 = not sold by this shop)
      "sell_price": 125,      // gold the shop pays (0 = not bought by this shop)
      "max_amount": 20        // per-transaction cap for buying and selling
    }
  ],
  "crafts": [
    {
      "item_id": 286,         // crafted result
      "ingredients": [        // max 4; extra entries are truncated at load
        { "item_id": 397, "amount": 3 },
        { "item_id": 339, "amount": 1 }
      ]
    }
  ]
}
```

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
inventories and prices are server-authored approximations - the original
Endless Online shop data was never published. Conventions used:

- sell price is always half of buy price (never an exploit);
- per-transaction `max_amount` shrinks for expensive goods (99 consumables,
  20 standard gear, 10 premium, 3 top-tier);
- `Dragon's Hoard` (vendor 40) requires level 10 to open, demonstrating the
  enforced level/class requirements.
