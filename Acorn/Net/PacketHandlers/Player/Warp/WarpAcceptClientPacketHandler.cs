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

    public async Task HandleAsync(PlayerConnection playerConnection,
        WarpAcceptClientPacket packet)
    {
        if (playerConnection.WarpSession is null)
        {
            _logger.LogError("Player connection has no WarpSession initialised.");
            return;
        }

        if (playerConnection.Character is null)
        {
            _logger.LogError("Player connection has no Character initialised.");
            return;
        }

        //todo: cancel any trades and whatnot if in progress
        var currentMap = _world.MapFor(playerConnection);
        if (currentMap is null)
        {
            _logger.LogError("Player connection is not in a valid map.");
            return;
        }

        await currentMap.Leave(playerConnection, playerConnection.WarpSession.WarpEffect);

        playerConnection.Character.Map = playerConnection.WarpSession.MapId;
        playerConnection.Character.X = playerConnection.WarpSession.X;
        playerConnection.Character.Y = playerConnection.WarpSession.Y;
        playerConnection.Character.SitState = SitState.Stand;

        if (playerConnection.WarpSession.Local)
        {
            await playerConnection.Send(new WarpAgreeServerPacket
            {
                Nearby = currentMap.AsNearbyInfo(),
                WarpType = WarpType.Local
            });

            await currentMap.Enter(playerConnection, playerConnection.WarpSession.WarpEffect);
            return;
        }

        var newMap = _world.MapFor(playerConnection);
        if (newMap is null)
        {
            _logger.LogError("Player connection is not in a valid map after warp.");
            return;
        }

        await newMap.Enter(playerConnection, playerConnection.WarpSession.WarpEffect);

        await playerConnection.Send(new WarpAgreeServerPacket
        {
            Nearby = newMap.AsNearbyInfo(),
            WarpType = WarpType.MapSwitch,
            WarpTypeData = new WarpAgreeServerPacket.WarpTypeDataMapSwitch
            {
                MapId = playerConnection.Character.Map,
                WarpEffect = playerConnection.WarpSession.WarpEffect
            }
        });

        playerConnection.WarpSession = null;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (WarpAcceptClientPacket)packet);
    }
}