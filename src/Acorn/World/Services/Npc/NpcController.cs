using Acorn.Extensions;
using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.World.Services.Npc;

public class NpcController : INpcController
{
    private const int CHASE_DISTANCE = 18; // matches eoserv's NPCChaseDistance
    private const int SPAWN_VARIANCE = 2;
    private const int MAX_SPAWN_ATTEMPTS = 200;
    private readonly IMapTileService _tileService;

    public NpcController(IMapTileService tileService)
    {
        _tileService = tileService;
    }

    public NpcMoveResult TryMove(NpcState npc, IEnumerable<PlayerState> players, IEnumerable<NpcState> npcs,
        Emf mapData)
    {
        var playerList = players.ToList();
        var npcList = npcs.ToList();

        // Stationary NPCs (spawn type 7) never move.
        if (npc.SpawnType == 7)
        {
            return new NpcMoveResult(false, npc.Direction, npc.AsCoords());
        }

        // Check if this NPC should chase a target.
        var targetCoords = GetChaseTarget(npc, playerList);
        var isChasing = targetCoords != null;

        Direction newDirection;

        if (isChasing)
        {
            // Chase mode - calculate direction toward target
            newDirection = GetChaseDirection(npc, targetCoords!);
        }
        else if (!TryGetIdleDirection(npc, out newDirection))
        {
            return new NpcMoveResult(false, npc.Direction, npc.AsCoords());
        }

        // Try primary direction
        if (TryMoveInDirection(npc, newDirection, playerList, npcList, mapData))
        {
            return new NpcMoveResult(true, npc.Direction, npc.AsCoords());
        }

        // If chasing and blocked, try an alternative direction, then a random direction.
        if (isChasing && targetCoords != null)
        {
            var altDirection = GetAlternativeChaseDirection(npc, targetCoords, newDirection);
            if (TryMoveInDirection(npc, altDirection, playerList, npcList, mapData))
            {
                return new NpcMoveResult(true, npc.Direction, npc.AsCoords());
            }

            // Last resort: try random direction
            if (TryMoveInDirection(npc, GetRandomDirection(), playerList, npcList, mapData))
            {
                return new NpcMoveResult(true, npc.Direction, npc.AsCoords());
            }
        }

        return new NpcMoveResult(false, npc.Direction, npc.AsCoords());
    }

    public (int X, int Y) FindSpawnPosition(NpcState npc, int baseX, int baseY,
        IEnumerable<PlayerState> players, IEnumerable<NpcState> npcs, Emf mapData)
    {
        var playerList = players.ToList();
        var npcList = npcs.ToList();

        // Tiles are 0-indexed, so the last valid coordinate is Width-1 / Height-1.
        var maxX = Math.Max(0, mapData.Width - 1);
        var maxY = Math.Max(0, mapData.Height - 1);

        var clampedX = Math.Clamp(baseX, 0, maxX);
        var clampedY = Math.Clamp(baseY, 0, maxY);

        // Track the last walkable candidate so a crowded spawn area still avoids
        // placing the NPC on a wall or outside the NPC boundary.
        var fallbackX = clampedX;
        var fallbackY = clampedY;

        for (var attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            var x = Math.Clamp(clampedX + Random.Shared.Next(-SPAWN_VARIANCE, SPAWN_VARIANCE + 1), 0, maxX);
            var y = Math.Clamp(clampedY + Random.Shared.Next(-SPAWN_VARIANCE, SPAWN_VARIANCE + 1), 0, maxY);

            // Skip unwalkable tiles (walls, NPC boundaries, edges, etc.)
            if (!IsTileWalkableForNpc(x, y, mapData))
            {
                continue;
            }

            // Remember this walkable position in case no empty one is found.
            fallbackX = x;
            fallbackY = y;

            // Avoid stacking on another NPC
            if (npcList.Any(n => n != npc && !n.IsDead && n.X == x && n.Y == y))
            {
                continue;
            }

            // Avoid spawning on top of a player
            if (playerList.Any(p => p.Character?.X == x && p.Character?.Y == y))
            {
                continue;
            }

            return (x, y);
        }

        // Never place an NPC on an unwalkable tile. If every candidate was occupied,
        // prefer the last walkable position over the base which may be a wall.
        return IsTileWalkableForNpc(fallbackX, fallbackY, mapData)
            ? (fallbackX, fallbackY)
            : (clampedX, clampedY);
    }

    /// <summary>
    ///     Whether the NPC should be placed within ±2 tiles of its spawn point with
    ///     collision avoidance. Matches eoserv, which spreads every NPC with spawn type
    ///     below 7 so multiple NPCs sharing a spawn point (common for quest/friendly NPCs
    ///     in EMF data) do not all stack on the same tile. Spawn type 7 is fixed to its
    ///     exact EMF position and direction.
    /// </summary>
    public bool ShouldUseSpawnVariance(NpcState npc)
    {
        return npc.SpawnType != 7;
    }

    public Direction GetSpawnDirection(NpcState npc)
    {
        // Fixed NPCs (SpawnType 7) use direction encoded in SpawnTime
        if (npc.SpawnType == 7)
        {
            return (Direction)(npc.SpawnTime & 0x03);
        }

        // Random direction for other NPCs
        return GetRandomDirection();
    }

    #region Private Chase Logic

    private Coords? GetChaseTarget(NpcState npc, List<PlayerState> players)
    {
        // If NPC has opponents, find the priority target (most damage dealt, in range)
        if (npc.Opponents.Count > 0)
        {
            var priorityOpponent = npc.Opponents
                .Where(o =>
                {
                    var player = players.FirstOrDefault(p => p.SessionId == o.PlayerId);
                    if (player?.Character == null || player.Character.Hidden)
                    {
                        return false;
                    }

                    var distance = Math.Abs(npc.X - player.Character.X) + Math.Abs(npc.Y - player.Character.Y);
                    return distance <= CHASE_DISTANCE;
                })
                .MaxBy(o => o.DamageDealt);

            if (priorityOpponent != null)
            {
                var targetPlayer = players.FirstOrDefault(p => p.SessionId == priorityOpponent.PlayerId);
                return targetPlayer?.Character?.AsCoords();
            }
        }

        // Aggressive NPCs with no opponents find the closest player
        if (npc.Data.Type == PubNpcType.Aggressive)
        {
            var closestPlayer = players
                .Where(p => p.Character != null && !p.Character.Hidden)
                .Select(p => new
                { Player = p, Distance = Math.Abs(npc.X - p.Character!.X) + Math.Abs(npc.Y - p.Character.Y) })
                .Where(x => x.Distance <= CHASE_DISTANCE)
                .MinBy(x => x.Distance);

            return closestPlayer?.Player.Character?.AsCoords();
        }

        return null;
    }

    private static Direction GetChaseDirection(NpcState npc, Coords targetCoords)
    {
        var xDelta = npc.X - targetCoords.X;
        var yDelta = npc.Y - targetCoords.Y;

        // Prefer the axis with the larger delta
        if (Math.Abs(xDelta) > Math.Abs(yDelta))
        {
            return xDelta < 0 ? Direction.Right : Direction.Left;
        }

        return yDelta < 0 ? Direction.Down : Direction.Up;
    }

    private static Direction GetAlternativeChaseDirection(NpcState npc, Coords targetCoords, Direction blockedDirection)
    {
        var xDelta = npc.X - targetCoords.X;
        var yDelta = npc.Y - targetCoords.Y;

        // If vertical was blocked, try horizontal
        if (blockedDirection == Direction.Up || blockedDirection == Direction.Down)
        {
            return xDelta < 0 ? Direction.Right : Direction.Left;
        }
        // If horizontal was blocked, try vertical

        return yDelta < 0 ? Direction.Down : Direction.Up;
    }

    #endregion

    #region Private Idle Movement Logic

    /// <summary>
    ///     Determine the NPC's idle movement direction, matching eoserv's random-walk
    ///     model: 60% walk forward, 30% walk in a random direction, 10% idle for 1-3 acts.
    ///     Returns false when the NPC should idle for this act.
    /// </summary>
    private static bool TryGetIdleDirection(NpcState npc, out Direction direction)
    {
        if (npc.WalkIdleFor > 0)
        {
            npc.WalkIdleFor--;
            direction = npc.Direction;
            return false;
        }

        var action = Random.Shared.Next(1, 11); // 1..10 inclusive

        if (action == 10)
        {
            npc.WalkIdleFor = Random.Shared.Next(1, 4); // idle for 1-3 acts
            direction = npc.Direction;
            return false;
        }

        // 1-6 (60%): keep direction and walk forward. 7-9 (30%): walk in a random direction.
        direction = action <= 6 ? npc.Direction : GetRandomDirection();
        return true;
    }

    private static Direction GetRandomDirection()
    {
        return (Direction)Random.Shared.Next(4);
    }

    #endregion

    #region Private Movement Execution

    private bool TryMoveInDirection(NpcState npc, Direction direction,
        List<PlayerState> players, List<NpcState> npcs, Emf mapData)
    {
        var nextCoords = npc.NextCoords(direction);

        // Boundary check (tiles are 0-indexed, so Width/Height are exclusive upper bounds)
        if (nextCoords.X < 0 || nextCoords.Y < 0 || nextCoords.X >= mapData.Width || nextCoords.Y >= mapData.Height)
        {
            return false;
        }

        // Check for player collision
        if (players.Any(x => x.Character?.AsCoords().Equals(nextCoords) == true))
        {
            return false;
        }

        // Check for NPC collision
        if (npcs.Any(x => x != npc && !x.IsDead && x.AsCoords().Equals(nextCoords)))
        {
            return false;
        }

        // Check tile walkability
        if (!IsTileWalkableForNpc(nextCoords.X, nextCoords.Y, mapData))
        {
            return false;
        }

        // Successfully move
        npc.X = nextCoords.X;
        npc.Y = nextCoords.Y;
        npc.Direction = direction;
        return true;
    }

    private bool IsTileWalkableForNpc(int x, int y, Emf mapData)
    {
        var row = mapData.TileSpecRows.Where(r => r.Y == y).ToList();
        var tile = row.SelectMany(r => r.Tiles).FirstOrDefault(t => t.X == x);

        if (tile is null)
        {
            return true; // No tile spec means walkable
        }

        return _tileService.IsNpcWalkable(tile.TileSpec);
    }

    #endregion
}
