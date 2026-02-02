using Acorn.Extensions;
using Acorn.Net;
using Acorn.Options;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.World.Services.Npc;

public class NpcController : INpcController
{
    private readonly IMapTileService _tileService;
    private readonly NpcOptions _npcOptions;

    public NpcController(IMapTileService tileService, IOptions<ServerOptions> serverOptions)
    {
        _tileService = tileService;
        _npcOptions = serverOptions.Value.Npc;
    }

    public NpcMoveResult TryMove(NpcState npc, IEnumerable<PlayerState> players, IEnumerable<NpcState> npcs,
        Emf mapData)
    {
        var playerList = players.ToList();
        var npcList = npcs.ToList();

        // Stationary NPCs never move
        if (npc.BehaviorType == NpcBehaviorType.Stationary)
        {
            return new NpcMoveResult(false, npc.Direction, npc.AsCoords());
        }

        // Check if this NPC should chase a target
        var targetCoords = GetChaseTarget(npc, playerList);
        var isChasing = targetCoords != null;

        Direction newDirection;

        if (isChasing)
        {
            // Chase mode - calculate direction toward target
            newDirection = GetChaseDirection(npc, targetCoords!);
        }
        else
        {
            // Idle movement - check if should move and get behavioral direction
            if (!ShouldMoveIdle(npc))
            {
                return new NpcMoveResult(false, npc.Direction, npc.AsCoords());
            }

            newDirection = GetIdleDirection(npc);
        }

        // Try primary direction
        if (TryMoveInDirection(npc, newDirection, playerList, npcList, mapData))
        {
            return new NpcMoveResult(true, npc.Direction, npc.AsCoords());
        }

        // If chasing and blocked, try alternative direction
        if (isChasing && targetCoords != null)
        {
            var altDirection = GetAlternativeChaseDirection(npc, targetCoords, newDirection);
            if (TryMoveInDirection(npc, altDirection, playerList, npcList, mapData))
            {
                return new NpcMoveResult(true, npc.Direction, npc.AsCoords());
            }

            // Last resort: try random direction
            var randomDirection = (Direction)Random.Shared.Next(4);
            if (TryMoveInDirection(npc, randomDirection, playerList, npcList, mapData))
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
        var variance = _npcOptions.SpawnVariance;
        var maxAttempts = _npcOptions.MaxSpawnAttempts;
        var relaxOccupancyAt = maxAttempts / 2; // After half attempts, relax occupancy check

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var x = Math.Clamp(baseX + Random.Shared.Next(-variance, variance + 1), 0, mapData.Width);
            var y = Math.Clamp(baseY + Random.Shared.Next(-variance, variance + 1), 0, mapData.Height);

            // Check if tile is walkable for NPCs
            if (!IsTileWalkableForNpc(x, y, mapData))
            {
                continue;
            }

            // Check if tile is occupied by another NPC
            var npcOccupied = npcList.Any(n => n != npc && !n.IsDead && n.X == x && n.Y == y);
            
            // Check if tile is occupied by a player
            var playerOccupied = playerList.Any(p => p.Character?.X == x && p.Character?.Y == y);

            // After half attempts, relax occupancy constraint (like reoserv)
            if (attempt >= relaxOccupancyAt)
            {
                if (!npcOccupied && !playerOccupied)
                {
                    return (x, y);
                }
            }
            else
            {
                // Normal strict checking
                if (!npcOccupied && !playerOccupied)
                {
                    return (x, y);
                }
            }
        }

        // Fallback to base spawn position if no valid position found
        return (baseX, baseY);
    }

    public bool ShouldUseSpawnVariance(NpcState npc)
    {
        return npc.SpawnType != 7 &&
               (npc.Data.Type == PubNpcType.Aggressive || npc.Data.Type == PubNpcType.Passive);
    }

    public Direction GetSpawnDirection(NpcState npc)
    {
        // Fixed NPCs (SpawnType 7) use direction encoded in SpawnTime
        if (npc.SpawnType == 7)
        {
            return (Direction)(npc.SpawnTime & 0x03);
        }

        // Random direction for other NPCs
        return (Direction)Random.Shared.Next(4);
    }

    public NpcBehaviorType DetermineBehaviorType(NpcState npc)
    {
        var data = npc.Data;

        // Guard NPCs (high HP, aggressive) tend to be stationary
        if (data.Hp > 500)
        {
            return Random.Shared.Next(100) < 40 ? NpcBehaviorType.Stationary : NpcBehaviorType.Patrol;
        }

        // Boss NPCs (very high HP) are more likely to patrol
        if (data.Hp > 1000)
        {
            return Random.Shared.Next(100) < 60 ? NpcBehaviorType.Patrol : NpcBehaviorType.Wander;
        }

        // Small/weak NPCs tend to wander
        if (data.Hp < 100)
        {
            return Random.Shared.Next(100) < 70 ? NpcBehaviorType.Wander : NpcBehaviorType.Circle;
        }

        // Default: Random behavior
        var behaviors = Enum.GetValues<NpcBehaviorType>();
        return behaviors[Random.Shared.Next(behaviors.Length)];
    }

    #region Private Chase Logic

    private Coords? GetChaseTarget(NpcState npc, List<PlayerState> players)
    {
        var chaseDistance = _npcOptions.ChaseDistance;
        
        // If NPC has opponents, find the priority target (most damage dealt, in range)
        if (npc.Opponents.Count > 0)
        {
            var priorityOpponent = npc.Opponents
                .Where(o =>
                {
                    var player = players.FirstOrDefault(p => p.SessionId == o.PlayerId);
                    if (player?.Character == null)
                    {
                        return false;
                    }

                    var distance = Math.Abs(npc.X - player.Character.X) + Math.Abs(npc.Y - player.Character.Y);
                    return distance <= chaseDistance;
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
                .Where(p => p.Character != null)
                .Select(p => new
                { Player = p, Distance = Math.Abs(npc.X - p.Character!.X) + Math.Abs(npc.Y - p.Character.Y) })
                .Where(x => x.Distance <= chaseDistance)
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

    private static bool ShouldMoveIdle(NpcState npc)
    {
        return npc.BehaviorType switch
        {
            NpcBehaviorType.Stationary => false,
            NpcBehaviorType.Wander => Random.Shared.Next(100) < 40,
            NpcBehaviorType.Patrol => Random.Shared.Next(100) < 60,
            NpcBehaviorType.Circle => Random.Shared.Next(100) < 50,
            _ => Random.Shared.Next(100) < 30
        };
    }

    private static Direction GetIdleDirection(NpcState npc)
    {
        var timeSinceLastChange = DateTime.UtcNow - npc.LastDirectionChange;

        switch (npc.BehaviorType)
        {
            case NpcBehaviorType.Stationary:
                return npc.Direction;

            case NpcBehaviorType.Wander:
                if (timeSinceLastChange.TotalSeconds > Random.Shared.Next(2, 6))
                {
                    npc.LastDirectionChange = DateTime.UtcNow;
                    if (Random.Shared.Next(100) < 30)
                    {
                        return npc.Direction;
                    }

                    return GetRandomDirection();
                }

                return npc.Direction;

            case NpcBehaviorType.Patrol:
                var distanceFromSpawn = Math.Abs(npc.X - npc.SpawnX) + Math.Abs(npc.Y - npc.SpawnY);
                if (distanceFromSpawn > 5)
                {
                    npc.LastDirectionChange = DateTime.UtcNow;
                    return GetOppositeDirection(npc.Direction);
                }

                if (timeSinceLastChange.TotalSeconds > Random.Shared.Next(3, 8))
                {
                    npc.LastDirectionChange = DateTime.UtcNow;
                    return GetRandomDirection();
                }

                return npc.Direction;

            case NpcBehaviorType.Circle:
                if (timeSinceLastChange.TotalSeconds > Random.Shared.Next(1, 3))
                {
                    npc.LastDirectionChange = DateTime.UtcNow;
                    return RotateDirectionClockwise(npc.Direction);
                }

                return npc.Direction;

            default:
                return GetRandomDirection();
        }
    }

    private static Direction GetRandomDirection()
    {
        return (Direction)Random.Shared.Next(4);
    }

    private static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => dir
        };
    }

    private static Direction RotateDirectionClockwise(Direction dir)
    {
        return dir switch
        {
            Direction.Up => Direction.Right,
            Direction.Right => Direction.Down,
            Direction.Down => Direction.Left,
            Direction.Left => Direction.Up,
            _ => dir
        };
    }

    #endregion

    #region Private Movement Execution

    private bool TryMoveInDirection(NpcState npc, Direction direction,
        List<PlayerState> players, List<NpcState> npcs, Emf mapData)
    {
        var nextCoords = npc.NextCoords(direction);

        // Boundary check
        if (nextCoords.X < 0 || nextCoords.Y < 0 || nextCoords.X > mapData.Width || nextCoords.Y > mapData.Height)
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