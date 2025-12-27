using Acorn.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Map;

/// <summary>
/// Service for broadcasting packets to players on a map.
/// </summary>
public interface IMapBroadcastService
{
    /// <summary>
    /// Broadcast a packet to all players, optionally excluding one.
    /// </summary>
    Task BroadcastPacket(IEnumerable<PlayerState> players, IPacket packet, PlayerState? except = null);

    /// <summary>
    /// Notify all players that a new player has entered the map.
    /// </summary>
    Task NotifyPlayerEnter(IEnumerable<PlayerState> players, PlayerState enteringPlayer,
        NearbyInfo nearbyInfo, WarpEffect warpEffect = WarpEffect.None);

    /// <summary>
    /// Notify all players that a player has left the map.
    /// </summary>
    Task NotifyPlayerLeave(IEnumerable<PlayerState> players, PlayerState leavingPlayer,
        WarpEffect warpEffect = WarpEffect.None);
}
