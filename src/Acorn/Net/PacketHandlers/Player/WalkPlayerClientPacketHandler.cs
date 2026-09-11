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
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
internal class WalkPlayerClientPacketHandler : IPacketHandler<WalkPlayerClientPacket>
{
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
        _world = world;
        _playerController = playerController;
        _mapTileService = mapTileService;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
    }

    public async Task HandleAsync(PlayerState playerState,
        WalkPlayerClientPacket packet)
    {
        // Frozen players cannot move
        if (playerState.IsFrozen)
        {
            await playerState.Send(new WalkCloseServerPacket());
            return;
        }

        // Sitting players cannot move (matches eoserv).
        if (playerState.Character!.SitState != SitState.Stand)
        {
            return;
        }

        playerState.Character!.X = packet.WalkAction.Direction switch
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

        // Get nearby NPCs for this player (within client range)
        var playerCoords = playerState.Character.AsCoords();
        var nearbyNpcIndexes = playerState.CurrentMap!.Npcs.Values
            .Where(npc => !npc.IsDead)
            .Where(npc => _mapTileService.InClientRange(playerCoords, new Coords { X = npc.X, Y = npc.Y }))
            .Select(npc => npc.Index)
            .ToList();

        // Get nearby players (within client range)
        var nearbyPlayerIds = playerState.CurrentMap.Players.Values
            .Where(p => p.Character != null && p.SessionId != playerState.SessionId)
            .Where(p => _mapTileService.InClientRange(playerCoords, p.Character!.AsCoords()))
            .Select(p => p.SessionId)
            .ToList();

        // Get nearby ground items (within client range) so items that came into
        // view are rendered without waiting for a refresh.
        var nearbyItems = playerState.CurrentMap.Items
            .Where(kvp => _mapTileService.InClientRange(playerCoords, kvp.Value.Coords))
            .Select(kvp => new ItemMapInfo
            {
                Uid = kvp.Key,
                Id = kvp.Value.Id,
                Coords = kvp.Value.Coords,
                Amount = kvp.Value.Amount
            })
            .ToList();

        // Send WalkReply to the walking player with nearby entities
        await playerState.Send(new WalkReplyServerPacket
        {
            PlayerIds = nearbyPlayerIds,
            NpcIndexes = nearbyNpcIndexes,
            Items = nearbyItems
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


    private bool TryGetWarpTile(MapState map, Acorn.Game.Models.Character character, out MapWarpRowTile? tile)
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