using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Npc;


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
    public int RespawnTimeSeconds { get; set; } = 60; // Default 60 seconds respawn

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