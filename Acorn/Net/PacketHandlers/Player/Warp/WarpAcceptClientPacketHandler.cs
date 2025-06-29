using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpAcceptClientPacketHandler : IPacketHandler<WarpAcceptClientPacket>
{
    private readonly ILogger<WarpAcceptClientPacketHandler> _logger;
    private readonly WorldState _world;

    public WarpAcceptClientPacketHandler(WorldState world, ILogger<WarpAcceptClientPacketHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        WarpAcceptClientPacket packet)
    {
        if (connectionHandler.WarpSession is null)
        {
            _logger.LogError("ConnectionHandler connection has no WarpSession initialised.");
            return;
        }

        if (connectionHandler.CharacterController is null)
        {
            _logger.LogError("ConnectionHandler connection has no Character initialised.");
            return;
        }

        //todo: cancel any trades and whatnot if in progress
        if (connectionHandler.CurrentMap is null)
        {
            _logger.LogError("ConnectionHandler connection is not in a valid map.");
            return;
        }

        await connectionHandler.CurrentMap.NotifyLeave(connectionHandler, connectionHandler.WarpSession.WarpEffect);

        connectionHandler.CharacterController.Data.Map = connectionHandler.WarpSession.MapId;
        connectionHandler.CharacterController.Data.X = connectionHandler.WarpSession.X;
        connectionHandler.CharacterController.Data.Y = connectionHandler.WarpSession.Y;
        connectionHandler.CharacterController.Data.SitState = SitState.Stand;

        await connectionHandler.CurrentMap.NotifyEnter(connectionHandler, connectionHandler.WarpSession.WarpEffect);

        if (connectionHandler.WarpSession.IsLocal)
        {
            await connectionHandler.Send(new WarpAgreeServerPacket
            {
                Nearby = connectionHandler.CurrentMap.AsNearbyInfo(),
                WarpType = WarpType.Local
            });
            return;
        }

        await connectionHandler.Send(new WarpAgreeServerPacket
        {
            Nearby = connectionHandler.CurrentMap.AsNearbyInfo(),
            WarpType = WarpType.MapSwitch,
            WarpTypeData = new WarpAgreeServerPacket.WarpTypeDataMapSwitch
            {
                MapId = connectionHandler.CharacterController.Data.Map,
                WarpEffect = connectionHandler.WarpSession.WarpEffect
            }
        });

        connectionHandler.WarpSession = null;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WarpAcceptClientPacket)packet);
    }
}