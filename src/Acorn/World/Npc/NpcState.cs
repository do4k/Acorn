using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Npc;

/// <summary>
///     Tracks a player who has attacked an NPC
/// </summary>
public class NpcOpponent
{
    public int PlayerId { get; set; }
    public int DamageDealt { get; set; }
    public int BoredTicks { get; set; }
}

/// <summary>
///     Represents the state of an NPC in the game world.
///     This is a pure data model - for control logic, use INpcController.
/// </summary>
public class NpcState
{
    public NpcState(EnfRecord data)
    {
        Data = data;
        Direction = Direction.Down;
        SpawnX = 0;
        SpawnY = 0;
        LastDirectionChange = DateTime.UtcNow;
    }

    // Core data
    public EnfRecord Data { get; set; }
    public int Id { get; set; }
    public int Hp { get; set; }

    // Position and movement
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; }
    public NpcBehaviorType BehaviorType { get; set; }
    public DateTime LastDirectionChange { get; set; }

    // Spawn data
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }

    /// <summary>
    ///     Spawn type from EMF file.
    ///     Type 7 = fixed position/direction (stationary NPCs like shopkeepers).
    ///     Other values allow variable spawn positions for aggressive/passive NPCs.
    /// </summary>
    public int SpawnType { get; set; }

    /// <summary>
    ///     Respawn time in seconds from EMF file.
    ///     For SpawnType 7, the lower 2 bits encode the fixed direction.
    /// </summary>
    public int SpawnTime { get; set; } = 60;

    /// <summary>
    ///     Whether this NPC was spawned by an admin command.
    ///     Admin-spawned NPCs should not respawn after being killed.
    /// </summary>
    public bool IsAdminSpawned { get; set; }

    /// <summary>
    ///     Gets the respawn time in seconds.
    ///     For SpawnType 7, respawn is typically instant (0) since they're fixed NPCs.
    ///     Admin-spawned NPCs never respawn.
    /// </summary>
    public int RespawnTimeSeconds => IsAdminSpawned ? -1 : SpawnType == 7 ? 0 : SpawnTime;

    // Death state
    public bool IsDead { get; set; }
    public DateTime? DeathTime { get; set; }

    // Combat/aggro tracking
    /// <summary>
    ///     List of players who have attacked this NPC.
    ///     Used for aggro targeting and damage tracking.
    /// </summary>
    public List<NpcOpponent> Opponents { get; set; } = new();

    /// <summary>
    ///     Ticks since last action (attack/move).
    /// </summary>
    public int ActTicks { get; set; }

    /// <summary>
    ///     Whether this NPC is a boss. Boss NPCs have special behavior and can have child minions.
    /// </summary>
    public bool IsBoss { get; set; }

    /// <summary>
    ///     Whether this NPC is a child minion of a boss. Child NPCs only spawn when their boss is alive.
    /// </summary>
    public bool IsChild { get; set; }

    #region Simple Data Operations

    /// <summary>
    ///     Add or update an opponent when they attack this NPC.
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
    ///     Remove opponents who have been "bored" (not attacked) for too long.
    /// </summary>
    public void DropBoredOpponents(int boredThreshold)
    {
        Opponents.RemoveAll(o => o.BoredTicks >= boredThreshold);
    }

    /// <summary>
    ///     Increment bored ticks for all opponents.
    /// </summary>
    public void IncrementBoredTicks(int amount)
    {
        foreach (var opponent in Opponents)
        {
            opponent.BoredTicks += amount;
        }
    }

    #endregion

    #region Simple Projections

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
        return NextCoords(Direction);
    }

    public Coords NextCoords(Direction direction)
    {
        var coords = AsCoords();
        return direction switch
        {
            Direction.Up => new Coords { X = coords.X, Y = coords.Y - 1 },
            Direction.Down => new Coords { X = coords.X, Y = coords.Y + 1 },
            Direction.Left => new Coords { X = coords.X - 1, Y = coords.Y },
            Direction.Right => new Coords { X = coords.X + 1, Y = coords.Y },
            _ => coords
        };
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

    #endregion
}