using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WalkPlayerClientPacketHandler : IPacketHandler<WalkPlayerClientPacket>
{
    private readonly ILogger<WalkPlayerClientPacketHandler> _logger;
    private readonly IMapTileService _mapTileService;
    private readonly IPlayerController _playerController;
    private readonly IWorldQueries _world;
    private readonly ICharacterCacheService _characterCache;
    private readonly IPaperdollService _paperdollService;

    public WalkPlayerClientPacketHandler(
        ILogger<WalkPlayerClientPacketHandler> logger,
        IWorldQueries world,
        IPlayerController playerController,
        IMapTileService mapTileService,
        ICharacterCacheService characterCache,
        IPaperdollService paperdollService)
    {
        _logger = logger;
        _world = world;
        _playerController = playerController;
        _mapTileService = mapTileService;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
    }

    public async Task HandleAsync(PlayerState playerState,
        WalkPlayerClientPacket packet)
    {
        if (playerState.Character is null)
        {
            _logger.LogError(
                "Tried to handler player walk, but the character associated with this connection has not been initialised");
            return;
        }

        playerState.Character.X = packet.WalkAction.Direction switch
        {
            Direction.Left => playerState.Character.X - 1,
            Direction.Right => playerState.Character.X + 1,
            _ => playerState.Character.X
        };

        playerState.Character.Y = packet.WalkAction.Direction switch
        {
            Direction.Up => playerState.Character.Y - 1,
            Direction.Down => playerState.Character.Y + 1,
            _ => playerState.Character.Y
        };

        playerState.Character.Direction = packet.WalkAction.Direction;

        // Cache character state after position/direction update
        await playerState.CacheCharacterStateAsync(_characterCache, _paperdollService);

        if (playerState.CurrentMap is null)
        {
            _logger.LogError(
                "Tried to handle walk player packet, but the map for the player connection was not found. MapId: {MapId}, PlayerId: {PlayerId}",
                playerState.Character.Map, playerState.SessionId);
            return;
        }

        // Get nearby NPCs for this player (within client range)
        var playerCoords = playerState.Character.AsCoords();
        var nearbyNpcIndexes = playerState.CurrentMap.Npcs
            .Select((npc, index) => new { Npc = npc, Index = index })
            .Where(x => _mapTileService.InClientRange(playerCoords, new Coords { X = x.Npc.X, Y = x.Npc.Y }))
            .Select(x => x.Index)
            .ToList();

        // Get nearby players (within client range)
        var nearbyPlayerIds = playerState.CurrentMap.Players
            .Where(p => p.Character != null && p.SessionId != playerState.SessionId)
            .Where(p => _mapTileService.InClientRange(playerCoords, p.Character!.AsCoords()))
            .Select(p => p.SessionId)
            .ToList();

        // Send WalkReply to the walking player with nearby entities
        await playerState.Send(new WalkReplyServerPacket
        {
            PlayerIds = nearbyPlayerIds,
            NpcIndexes = nearbyNpcIndexes,
            Items = new List<ItemMapInfo>() // TODO: Add nearby items when item system is implemented
        });

        // Broadcast WalkPlayer to other players on the map
        await playerState.CurrentMap.BroadcastPacket(new WalkPlayerServerPacket
        {
            Direction = playerState.Character.Direction,
            PlayerId = playerState.SessionId,
            Coords = new Coords
            {
                X = playerState.Character.X,
                Y = playerState.Character.Y
            }
        }, playerState);

        var hasWarp = TryGetWarpTile(playerState.CurrentMap, playerState.Character, out var warpTile);
        if (hasWarp is false || warpTile is null)
        {
            return;
        }

        var targetMap = _world.FindMap(warpTile.Warp.DestinationMap);
        if (targetMap is null)
        {
            return;
        }

        await _playerController.WarpAsync(
            playerState,
            targetMap,
            warpTile.Warp.DestinationCoords.X,
            warpTile.Warp.DestinationCoords.Y);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (WalkPlayerClientPacket)packet);
    }

    private bool TryGetWarpTile(MapState map, Acorn.Domain.Models.Character character, out MapWarpRowTile? tile)
    {
        var possibleY = map.Data.WarpRows.Where(wr => wr.Y == character.Y);
        var mapWarpRows = possibleY as MapWarpRow[] ?? possibleY.ToArray();
        if (mapWarpRows.Any() is false)
        {
            tile = null;
            return false;
        }

        var possibleX = mapWarpRows.SelectMany(wr => wr.Tiles.Where(tile => tile.X == character.X));
        var mapWarpRowTiles = possibleX as MapWarpRowTile[] ?? possibleX.ToArray();
        if (mapWarpRowTiles.Any() is false)
        {
            tile = null;
            return false;
        }

        var warpTile = mapWarpRowTiles.FirstOrDefault();
        tile = warpTile;
        return warpTile is not null;
    }
}