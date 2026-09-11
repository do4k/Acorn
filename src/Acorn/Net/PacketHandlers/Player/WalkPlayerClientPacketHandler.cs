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

        var direction = packet.WalkAction.Direction;

        var targetX = playerState.Character!.X + (direction switch
        {
            Direction.Left => -1,
            Direction.Right => 1,
            _ => 0
        });

        var targetY = playerState.Character.Y + (direction switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        });

        // A warp tile takes precedence over a normal step (matches eoserv map.cpp:921-943).
        // Validate before moving so a blocked warp leaves the player on their current tile.
        var warpTile = TryGetWarpTile(playerState.CurrentMap!, targetX, targetY);
        if (warpTile is not null)
        {
            var targetMap = _world.FindMap(warpTile.Warp.DestinationMap);
            if (targetMap is not null && IsWarpUsable(playerState, playerState.CurrentMap!, warpTile, targetX, targetY))
            {
                await _playerController.WarpAsync(
                    playerState,
                    targetMap,
                    warpTile.Warp.DestinationCoords.X,
                    warpTile.Warp.DestinationCoords.Y);
            }

            return;
        }

        playerState.Character.X = targetX;
        playerState.Character.Y = targetY;
        playerState.Character.Direction = direction;

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
    }

    /// <summary>
    ///     A warp tile is only usable when the character meets its level requirement and,
    ///     for door warps, the door is currently open. Door keys are not yet modelled.
    /// </summary>
    private static bool IsWarpUsable(PlayerState player, MapState map, MapWarpRowTile tile, int x, int y)
    {
        var warp = tile.Warp;

        if (player.Character is null || player.Character.Level < warp.LevelRequired)
        {
            return false;
        }

        var isDoor = warp.Door != 0;
        if (isDoor && !map.OpenedDoors.ContainsKey(new Coords { X = x, Y = y }))
        {
            return false;
        }

        return true;
    }

    private static MapWarpRowTile? TryGetWarpTile(MapState map, int x, int y)
    {
        return map.Data.WarpRows
            .Where(row => row.Y == y)
            .SelectMany(row => row.Tiles)
            .FirstOrDefault(tile => tile.X == x && tile.Warp is not null);
    }
}
