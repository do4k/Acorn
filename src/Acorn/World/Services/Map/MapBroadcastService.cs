using Acorn.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Map;

public class MapBroadcastService : IMapBroadcastService
{
    public async Task BroadcastPacket(IEnumerable<PlayerState> players, IPacket packet, PlayerState? except = null)
    {
        var tasks = players
            .Where(p => except is null || p != except)
            .Select(async player => await player.Send(packet));

        await Task.WhenAll(tasks);
    }

    public async Task NotifyPlayerEnter(IEnumerable<PlayerState> players, PlayerState enteringPlayer,
        NearbyInfo nearbyInfo, WarpEffect warpEffect = WarpEffect.None)
    {
        await BroadcastPacket(players, new PlayersAgreeServerPacket
        {
            Nearby = nearbyInfo
        }, enteringPlayer);
    }

    public async Task NotifyPlayerLeave(IEnumerable<PlayerState> players, PlayerState leavingPlayer,
        WarpEffect warpEffect = WarpEffect.None)
    {
        var playerRemoveTask = BroadcastPacket(players, new PlayersRemoveServerPacket
        {
            PlayerId = leavingPlayer.SessionId
        }, leavingPlayer);

        var avatarRemoveTask = BroadcastPacket(players, new AvatarRemoveServerPacket
        {
            PlayerId = leavingPlayer.SessionId,
            WarpEffect = warpEffect
        }, leavingPlayer);

        await Task.WhenAll(playerRemoveTask, avatarRemoveTask);
    }
}