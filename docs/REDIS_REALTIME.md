# Real-Time Game State in Redis

## Overview
This document describes how real-time game state (map data, NPC health, player positions) is stored in Redis for use by the Acorn API.

## Redis Key Structure
- `map:{mapId}:state` — JSON object containing map state, including:
  - NPCs: health, position, status
  - Players: x, y, name, status

## Example Value
```
{
  "npcs": [
    { "id": 1, "name": "Slime", "hp": 12, "x": 5, "y": 7 },
    { "id": 2, "name": "Bat", "hp": 8, "x": 10, "y": 3 }
  ],
  "players": [
    { "id": 101, "name": "Alice", "x": 6, "y": 8 },
    { "id": 102, "name": "Bob", "x": 7, "y": 8 }
  ]
}
```

## API Endpoint
- `GET /api/map/{mapId}/state` — Returns the current state for a map from Redis.

## Notes
- This data is not persisted in the main database.
- Game server should update these keys in real time.
- API is read-only for now, but can be extended for admin tools or live dashboards.

