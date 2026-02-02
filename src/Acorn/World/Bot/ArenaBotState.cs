using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Bot;

/// <summary>
///     Represents the state of an arena bot in the game world.
///     Bots are AI-controlled entities that participate in arena matches.
/// </summary>
public class ArenaBotState
{
    /// <summary>
    ///     Unique identifier for this bot instance.
    /// </summary>
    public required int Id { get; set; }

    /// <summary>
    ///     Display name of the bot (includes [BOT] prefix).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Current X coordinate on the map.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Current Y coordinate on the map.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Current facing direction.
    /// </summary>
    public Direction Direction { get; set; } = Direction.Down;

    /// <summary>
    ///     Map ID where this bot is active.
    /// </summary>
    public int MapId { get; set; }

    /// <summary>
    ///     Ticks since last action (attack/move).
    ///     Used to control bot action timing.
    /// </summary>
    public int ActTicks { get; set; }

    /// <summary>
    ///     Target bot ID for combat tracking (0 if no target).
    /// </summary>
    public int TargetBotId { get; set; }

    /// <summary>
    ///     Whether this bot is currently in an active arena match.
    /// </summary>
    public bool IsInArena { get; set; }

    /// <summary>
    ///     Spawn index from arena configuration (used for queue/battle positions).
    /// </summary>
    public int SpawnIndex { get; set; }

    #region Helper Methods

    /// <summary>
    ///     Returns the bot's current position as Coords.
    /// </summary>
    public Coords AsCoords()
    {
        return new Coords { X = X, Y = Y };
    }

    /// <summary>
    ///     Calculates the next coordinate in the given direction.
    /// </summary>
    public Coords NextCoords(Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Coords { X = X, Y = Y - 1 },
            Direction.Down => new Coords { X = X, Y = Y + 1 },
            Direction.Left => new Coords { X = X - 1, Y = Y },
            Direction.Right => new Coords { X = X + 1, Y = Y },
            _ => AsCoords()
        };
    }

    /// <summary>
    ///     Calculates Manhattan distance to another bot.
    /// </summary>
    public int DistanceTo(ArenaBotState other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    }

    /// <summary>
    ///     Calculates Manhattan distance to a coordinate.
    /// </summary>
    public int DistanceTo(Coords coords)
    {
        return Math.Abs(X - coords.X) + Math.Abs(Y - coords.Y);
    }

    #endregion
}
