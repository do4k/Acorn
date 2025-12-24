using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Npc;

/// <summary>
/// Tracks a player who has attacked an NPC
/// </summary>
public class NpcOpponent
{
    public int PlayerId { get; set; }
    public int DamageDealt { get; set; }
    public int BoredTicks { get; set; }
}

public class NpcState
{
    private static readonly Random _random = new Random();
    private static readonly Direction[] _directions = Enum.GetValues<Direction>();

    public NpcState(EnfRecord data)
    {
        Data = data;
        BehaviorType = DetermineBehaviorType(data);
        Direction = GetRandomDirection();
        SpawnX = 0;
        SpawnY = 0;
        LastDirectionChange = DateTime.UtcNow;
    }

    public EnfRecord Data { get; set; }
    public Direction Direction { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Id { get; set; }
    public int Hp { get; set; }
    public NpcBehaviorType BehaviorType { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public DateTime LastDirectionChange { get; set; }
    public bool IsDead { get; set; }
    public DateTime? DeathTime { get; set; }

    /// <summary>
    /// Spawn type from EMF file.
    /// Type 7 = fixed position/direction (stationary NPCs like shopkeepers).
    /// Other values allow variable spawn positions for aggressive/passive NPCs.
    /// </summary>
    public int SpawnType { get; set; }

    /// <summary>
    /// Respawn time in seconds from EMF file.
    /// For SpawnType 7, the lower 2 bits encode the fixed direction.
    /// </summary>
    public int SpawnTime { get; set; } = 60;

    /// <summary>
    /// Gets the respawn time in seconds.
    /// For SpawnType 7, respawn is typically instant (0) since they're fixed NPCs.
    /// </summary>
    public int RespawnTimeSeconds => SpawnType == 7 ? 0 : SpawnTime;

    /// <summary>
    /// List of players who have attacked this NPC.
    /// Used for aggro targeting and damage tracking.
    /// </summary>
    public List<NpcOpponent> Opponents { get; set; } = new();

    /// <summary>
    /// Ticks since last action (attack/move).
    /// </summary>
    public int ActTicks { get; set; }

    /// <summary>
    /// Add or update an opponent when they attack this NPC.
    /// </summary>
    public void AddOpponent(int playerId, int damage)
    {
        var existing = Opponents.FirstOrDefault(o => o.PlayerId == playerId);
        if (existing != null)
        {
            existing.DamageDealt += damage;
            existing.BoredTicks = 0;
        }
        else
        {
            Opponents.Add(new NpcOpponent
            {
                PlayerId = playerId,
                DamageDealt = damage,
                BoredTicks = 0
            });
        }
    }

    /// <summary>
    /// Remove opponents who have been "bored" (not attacked) for too long.
    /// </summary>
    public void DropBoredOpponents(int boredThreshold)
    {
        Opponents.RemoveAll(o => o.BoredTicks >= boredThreshold);
    }

    /// <summary>
    /// Increment bored ticks for all opponents.
    /// </summary>
    public void IncrementBoredTicks(int amount)
    {
        foreach (var opponent in Opponents)
        {
            opponent.BoredTicks += amount;
        }
    }

    public Coords AsCoords()
    {
        return new Coords
        {
            X = X,
            Y = Y
        };
    }

    public Coords NextCoords()
    {
        return AsCoords().NextCoords(Direction);
    }

    public Coords NextCoords(Direction direction)
    {
        return AsCoords().NextCoords(direction);
    }

    public NpcMapInfo AsNpcMapInfo(int index)
    {
        return new NpcMapInfo
        {
            Coords = new Coords { X = X, Y = Y },
            Direction = Direction,
            Id = Id,
            Index = index
        };
    }

    /// <summary>
    /// Determine NPC behavior based on NPC type or characteristics
    /// </summary>
    private static NpcBehaviorType DetermineBehaviorType(EnfRecord data)
    {
        // Guard NPCs (high HP, aggressive) tend to be stationary
        if (data.Hp > 500)
        {
            return _random.Next(100) < 40 ? NpcBehaviorType.Stationary : NpcBehaviorType.Patrol;
        }

        // Boss NPCs (very high HP) are more likely to patrol
        if (data.Hp > 1000)
        {
            return _random.Next(100) < 60 ? NpcBehaviorType.Patrol : NpcBehaviorType.Wander;
        }

        // Small/weak NPCs tend to wander
        if (data.Hp < 100)
        {
            return _random.Next(100) < 70 ? NpcBehaviorType.Wander : NpcBehaviorType.Circle;
        }

        // Default: Random behavior
        var behaviors = Enum.GetValues<NpcBehaviorType>();
        return behaviors[_random.Next(behaviors.Length)];
    }

    /// <summary>
    /// Get next direction based on NPC behavior
    /// </summary>
    public Direction GetNextDirection()
    {
        var timeSinceLastChange = DateTime.UtcNow - LastDirectionChange;

        switch (BehaviorType)
        {
            case NpcBehaviorType.Stationary:
                return Direction; // Don't change direction

            case NpcBehaviorType.Wander:
                // Change direction randomly every 2-5 seconds
                if (timeSinceLastChange.TotalSeconds > _random.Next(2, 6))
                {
                    LastDirectionChange = DateTime.UtcNow;
                    // 30% chance to keep same direction, 70% chance to change
                    if (_random.Next(100) < 30)
                        return Direction;
                    return GetRandomDirection();
                }
                return Direction;

            case NpcBehaviorType.Patrol:
                // Patrol back and forth - check if we've moved too far from spawn
                var distanceFromSpawn = Math.Abs(X - SpawnX) + Math.Abs(Y - SpawnY);
                if (distanceFromSpawn > 5)
                {
                    // Turn around and head back
                    LastDirectionChange = DateTime.UtcNow;
                    return GetOppositeDirection(Direction);
                }
                // Change direction occasionally
                if (timeSinceLastChange.TotalSeconds > _random.Next(3, 8))
                {
                    LastDirectionChange = DateTime.UtcNow;
                    return GetRandomDirection();
                }
                return Direction;

            case NpcBehaviorType.Circle:
                // Rotate direction clockwise every 1-2 seconds
                if (timeSinceLastChange.TotalSeconds > _random.Next(1, 3))
                {
                    LastDirectionChange = DateTime.UtcNow;
                    return RotateDirectionClockwise(Direction);
                }
                return Direction;

            default:
                return GetRandomDirection();
        }
    }

    /// <summary>
    /// Should this NPC attempt to move this tick?
    /// </summary>
    public bool ShouldMove()
    {
        return BehaviorType switch
        {
            NpcBehaviorType.Stationary => false,// Never move
            NpcBehaviorType.Wander => _random.Next(100) < 40,// 40% chance per tick
            NpcBehaviorType.Patrol => _random.Next(100) < 60,// 60% chance per tick
            NpcBehaviorType.Circle => _random.Next(100) < 50,// 50% chance per tick
            _ => _random.Next(100) < 30,
        };
    }

    private static Direction GetRandomDirection()
    {
        return _directions[_random.Next(_directions.Length)];
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
}