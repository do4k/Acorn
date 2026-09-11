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

        // The map change already happened in PlayerController.WarpAsync, so this handler
        // only acknowledges the warp. The session is always cleared, local warps included.
        try
        {
            if (warpSession.IsLocal)
            {
                await playerState.Send(new WarpAgreeServerPacket
                {
                    Nearby = warpSession.TargetMap.AsNearbyInfo(),
                    WarpType = WarpType.Local
                });
                return;
            }

            await playerState.Send(new WarpAgreeServerPacket
            {
                Nearby = warpSession.TargetMap.AsNearbyInfo(),
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
