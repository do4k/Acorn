using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpAcceptClientPacketHandler : IPacketHandler<WarpAcceptClientPacket>
{
    private readonly ILogger<WarpAcceptClientPacketHandler> _logger;

    public WarpAcceptClientPacketHandler(IWorldQueries world, ILogger<WarpAcceptClientPacketHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        WarpAcceptClientPacket packet)
    {
        if (playerState.WarpSession is null)
        {
            _logger.LogError("Player connection has no WarpSession initialised.");
            return;
        }

        if (playerState.Character is null)
        {
            _logger.LogError("Player connection has no Character initialised.");
            return;
        }

        //todo: cancel any trades and whatnot if in progress
        if (playerState.CurrentMap is null)
        {
            _logger.LogError("Player connection is not in a valid map.");
            return;
        }

        await playerState.CurrentMap.NotifyLeave(playerState, playerState.WarpSession.WarpEffect);

        playerState.Character.Map = playerState.WarpSession.MapId;
        playerState.Character.X = playerState.WarpSession.X;
        playerState.Character.Y = playerState.WarpSession.Y;
        playerState.Character.SitState = SitState.Stand;

        await playerState.CurrentMap.NotifyEnter(playerState, playerState.WarpSession.WarpEffect);

        if (playerState.WarpSession.IsLocal)
        {
            await playerState.Send(new WarpAgreeServerPacket
            {
                Nearby = playerState.CurrentMap.AsNearbyInfo(),
                WarpType = WarpType.Local
            });
            return;
        }

        await playerState.Send(new WarpAgreeServerPacket
        {
            Nearby = playerState.CurrentMap.AsNearbyInfo(),
            WarpType = WarpType.MapSwitch,
            WarpTypeData = new WarpAgreeServerPacket.WarpTypeDataMapSwitch
            {
                MapId = playerState.Character.Map,
                WarpEffect = playerState.WarpSession.WarpEffect
            }
        });

        playerState.WarpSession = null;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (WarpAcceptClientPacket)packet);
    }
}