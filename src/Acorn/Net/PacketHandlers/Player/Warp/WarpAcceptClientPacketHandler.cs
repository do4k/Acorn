using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

public class WarpAcceptClientPacketHandler : IPacketHandler<WarpAcceptClientPacket>
{
    private readonly ILogger<WarpAcceptClientPacketHandler> _logger;
    private readonly ICharacterCacheService _characterCache;
    private readonly IPaperdollService _paperdollService;

    public WarpAcceptClientPacketHandler(
        IWorldQueries world,
        ILogger<WarpAcceptClientPacketHandler> logger,
        ICharacterCacheService characterCache,
        IPaperdollService paperdollService)
    {
        _logger = logger;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
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

        // Cache character state after warp position update
        await playerState.CacheCharacterStateAsync(_characterCache, _paperdollService);

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

}