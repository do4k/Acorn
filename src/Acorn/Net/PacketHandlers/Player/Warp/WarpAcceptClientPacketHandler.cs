using Acorn.Net.PacketHandlers;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

[RequiresCharacter]
public class WarpAcceptClientPacketHandler : IPacketHandler<WarpAcceptClientPacket>
{
    private readonly ILogger<WarpAcceptClientPacketHandler> _logger;

    public WarpAcceptClientPacketHandler(
        ILogger<WarpAcceptClientPacketHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        WarpAcceptClientPacket packet)
    {
        var warpSession = playerState.WarpSession;
        if (warpSession is null)
        {
            _logger.LogError("Player connection has no WarpSession initialised.");
            return;
        }

        //todo: cancel any trades and whatnot if in progress

        // The map change (leave old map / enter new map) already happened in
        // PlayerController.WarpAsync, matching eoserv's Character::Warp. The session is always
        // cleared, local warps included. This handler only acknowledges the warp with the
        // range-filtered nearby list for the new position.
        try
        {
            if (warpSession.IsLocal)
            {
                await playerState.Send(new WarpAgreeServerPacket
                {
                    Nearby = warpSession.TargetMap.AsNearbyInfo(playerState),
                    WarpType = WarpType.Local
                });
                return;
            }

            await playerState.Send(new WarpAgreeServerPacket
            {
                Nearby = warpSession.TargetMap.AsNearbyInfo(playerState),
                WarpType = WarpType.MapSwitch,
                WarpTypeData = new WarpAgreeServerPacket.WarpTypeDataMapSwitch
                {
                    MapId = warpSession.MapId,
                    WarpEffect = warpSession.WarpEffect
                }
            });
        }
        finally
        {
            playerState.WarpSession = null;
        }
    }
}
