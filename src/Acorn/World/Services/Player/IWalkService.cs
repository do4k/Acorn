using Acorn.Net;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Player;

/// <summary>
///     Handles player walking: validation (bounds/collision/occupancy), client
///     desync correction, interaction/trade cleanup and the walk packets.
/// </summary>
public interface IWalkService
{
    /// <summary>
    ///     Attempt to move the player one tile in <paramref name="direction" />.
    /// </summary>
    /// <param name="player">The walking player.</param>
    /// <param name="direction">The direction the client wants to move.</param>
    /// <param name="timestamp">The client-supplied walk timestamp.</param>
    /// <param name="reportedCoords">The destination the client believes it is moving to.</param>
    /// <param name="admin">
    ///     When true, collision/occupancy checks are skipped (admin #nowall).
    /// </param>
    Task WalkAsync(PlayerState player, Direction direction, int timestamp, Coords reportedCoords, bool admin = false);
}
