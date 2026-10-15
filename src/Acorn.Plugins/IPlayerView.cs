namespace Acorn.Plugins;

/// <summary>
///     Read-only view of an online player. Properties reflect live server state;
///     values are defaults (0 / null) while the connection has no character in
///     the world.
/// </summary>
public interface IPlayerView
{
    /// <summary>Connection session id (the player's identity key while online).</summary>
    int SessionId { get; }

    /// <summary>Character name, or null before a character is loaded.</summary>
    string? Name { get; }

    /// <summary>Current map id, or 0 if not in the world.</summary>
    int MapId { get; }

    /// <summary>Current map X coordinate.</summary>
    int X { get; }

    /// <summary>Current map Y coordinate.</summary>
    int Y { get; }

    /// <summary>Character level.</summary>
    int Level { get; }

    /// <summary>Current hit points.</summary>
    int Hp { get; }

    /// <summary>Maximum hit points.</summary>
    int MaxHp { get; }
}
