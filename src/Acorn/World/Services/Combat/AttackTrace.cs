using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Services.Combat;

/// <summary>
///     Resolves the tile a player's attack reaches by tracing outward from the
///     attacker in the packet's direction. Mirrors eoserv's Map::Attack: the
///     first occupied tile is the target, and an unwalkable tile stops the trace.
/// </summary>
public static class AttackTrace
{
    /// <summary>
    ///     Resolves the attack range for an equipped weapon: <paramref name="rangedDistance" />
    ///     for ranged weapons, otherwise 1 (melee).
    /// </summary>
    public static int GetRange(ItemSubtype? weaponSubtype, int rangedDistance)
    {
        return weaponSubtype == ItemSubtype.Ranged ? rangedDistance : 1;
    }

    /// <summary>
    ///     Walks from <paramref name="origin" /> in <paramref name="direction" /> for up to
    ///     <paramref name="range" /> tiles and returns the first occupied tile. Returns
    ///     <c>null</c> when nothing is hit before the range ends or an obstacle is reached.
    /// </summary>
    /// <param name="origin">The attacker's tile.</param>
    /// <param name="direction">The direction the attack travels.</param>
    /// <param name="range">Maximum number of tiles to trace (1 for melee).</param>
    /// <param name="isOccupied">Whether a tile holds a valid target.</param>
    /// <param name="isBlocked">Whether a tile blocks the attack (e.g. a wall).</param>
    public static Coords? FindTargetTile(
        Coords origin,
        Direction direction,
        int range,
        Func<Coords, bool> isOccupied,
        Func<Coords, bool> isBlocked)
    {
        var current = origin;

        for (var step = 0; step < range; step++)
        {
            current = current.NextCoords(direction);

            if (isOccupied(current))
            {
                return current;
            }

            if (isBlocked(current))
            {
                return null;
            }
        }

        return null;
    }
}
