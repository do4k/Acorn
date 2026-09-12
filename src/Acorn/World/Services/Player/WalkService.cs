using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Player;

/// <summary>
///     Validates and applies player movement. Mirrors eoserv's
///     <c>Handlers::walk_common</c> / <c>Map::Walk</c>: the server is authoritative
///     and refuses moves into out-of-bounds, unwalkable or occupied tiles, then
///     forces a refresh so a desynced client is snapped back.
/// </summary>
public class WalkService : IWalkService
{
    // eoserv rejects walk packets that arrive less than this many timestamp units
    // after the previous one (handlers/Walk.cpp:28).
    private const int MinWalkTimestampDelta = 36;

    // eoserv's Timestamp wraps at this value (character.hpp max_ts).
    private const int MaxTimestamp = 8640000;

    private readonly ICharacterCacheService _characterCache;
    private readonly ILogger<WalkService> _logger;
    private readonly IMapTileService _mapTileService;
    private readonly IPaperdollService _paperdollService;
    private readonly IPlayerController _playerController;
    private readonly ServerOptions _serverOptions;
    private readonly IWorldQueries _world;

    public WalkService(
        ILogger<WalkService> logger,
        IWorldQueries world,
        IPlayerController playerController,
        IMapTileService mapTileService,
        ICharacterCacheService characterCache,
        IPaperdollService paperdollService,
        IOptions<ServerOptions> serverOptions)
    {
        _logger = logger;
        _world = world;
        _playerController = playerController;
        _mapTileService = mapTileService;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
        _serverOptions = serverOptions.Value;
    }

    public async Task WalkAsync(PlayerState player, Direction direction, int timestamp, Coords reportedCoords,
        bool admin = false)
    {
        // Frozen players cannot move.
        if (player.IsFrozen)
        {
            await player.Send(new WalkCloseServerPacket());
            return;
        }

        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        // Timestamp validation (eoserv EnforceTimestamps). Ignore packets that
        // arrive too soon after the previous one.
        if (_serverOptions.EnforceTimestamps &&
            TimestampDiff(timestamp, player.Timestamp) < MinWalkTimestampDelta)
        {
            return;
        }

        player.Timestamp = timestamp;

        // Sitting players cannot move (matches eoserv).
        if (player.Character.SitState != SitState.Stand)
        {
            return;
        }

        if (!TryGetTargetCoords(player.Character, direction, out var target))
        {
            // Unknown direction: treat as a failed walk so the client resyncs.
            await _playerController.RefreshAsync(player);
            return;
        }

        // A warp tile takes precedence over a normal step (matches eoserv
        // map.cpp:921-943). Validate before moving so a blocked warp leaves the
        // player on their current tile.
        var warpTile = TryGetWarpTile(player.CurrentMap, target.X, target.Y);
        if (warpTile is not null)
        {
            var warpTargetMap = _world.FindMap(warpTile.Warp.DestinationMap);
            if (warpTargetMap is not null &&
                IsWarpUsable(player, player.CurrentMap, warpTile, target.X, target.Y))
            {
                await ClearInteractionsAsync(player);
                await _playerController.WarpAsync(
                    player,
                    warpTargetMap,
                    warpTile.Warp.DestinationCoords.X,
                    warpTile.Warp.DestinationCoords.Y);
            }

            return;
        }

        // Moving always drops any open NPC/board/chest interaction and cancels an
        // active trade (eoserv handlers/Walk.cpp:41-67).
        await ClearInteractionsAsync(player);

        if (!admin && !IsDestinationValid(player.CurrentMap, target))
        {
            _logger.LogDebug(
                "Rejected walk for {Character} to ({X}, {Y}) on map {MapId} (blocked or out of bounds)",
                player.Character.Name, target.X, target.Y, player.CurrentMap.Id);

            // Snap the client back to the server-authoritative position.
            await _playerController.RefreshAsync(player);
            return;
        }

        var oldCoords = player.Character.AsCoords();

        player.Character.X = target.X;
        player.Character.Y = target.Y;
        player.Character.Direction = direction;

        // Cache character state after position/direction update.
        await player.CacheCharacterStateAsync(_characterCache, _paperdollService);

        var playerCoords = player.Character.AsCoords();

        // Get nearby NPCs for this player (within client range).
        var nearbyNpcIndexes = player.CurrentMap.Npcs.Values
            .Where(npc => !npc.IsDead)
            .Where(npc => _mapTileService.InClientRange(playerCoords, new Coords { X = npc.X, Y = npc.Y }))
            .Select(npc => npc.Index)
            .ToList();

        // Get nearby players (within client range).
        var nearbyPlayerIds = player.CurrentMap.Players.Values
            .Where(p => p.Character != null && p.SessionId != player.SessionId)
            .Where(p => _mapTileService.InClientRange(playerCoords, p.Character!.AsCoords()))
            .Select(p => p.SessionId)
            .ToList();

        // Get nearby ground items (within client range) so items that came into
        // view are rendered without waiting for a refresh.
        var nearbyItems = player.CurrentMap.Items
            .Where(kvp => _mapTileService.InClientRange(playerCoords, kvp.Value.Coords))
            .Select(kvp => new ItemMapInfo
            {
                Uid = kvp.Key,
                Id = kvp.Value.Id,
                Coords = kvp.Value.Coords,
                Amount = kvp.Value.Amount
            })
            .ToList();

        // Send WalkReply to the walking player with nearby entities.
        await player.Send(new WalkReplyServerPacket
        {
            PlayerIds = nearbyPlayerIds,
            NpcIndexes = nearbyNpcIndexes,
            Items = nearbyItems
        });

        // Broadcast WalkPlayer only to players who can see the new position.
        var walkPacket = new WalkPlayerServerPacket
        {
            Direction = player.Character.Direction,
            PlayerId = player.SessionId,
            Coords = new Coords
            {
                X = player.Character.X,
                Y = player.Character.Y
            }
        };

        var recipients = player.CurrentMap.Players.Values
            .Where(p => p.SessionId != player.SessionId && p.Character is not null)
            .Where(p => _mapTileService.InClientRange(playerCoords, p.Character!.AsCoords()))
            .ToList();

        foreach (var recipient in recipients)
        {
            await recipient.Send(walkPacket);
        }

        // Remove players/NPCs that just fell out of view because of this step.
        await player.CurrentMap.NotifyMoveViewChangesAsync(player, oldCoords);

        // If the server-authoritative position differs from what the client
        // reported, the client is desynced; force a refresh (eoserv Walk.cpp:72-75).
        if (player.Character.X != reportedCoords.X || player.Character.Y != reportedCoords.Y)
        {
            _logger.LogDebug(
                "Walk desync for {Character}: server ({ServerX}, {ServerY}) != client ({ClientX}, {ClientY})",
                player.Character.Name, player.Character.X, player.Character.Y,
                reportedCoords.X, reportedCoords.Y);

            await _playerController.RefreshAsync(player);
        }
    }

    /// <summary>
    ///     Whether the destination tile is inside the map, walkable and free of
    ///     other players/NPCs.
    /// </summary>
    private bool IsDestinationValid(MapState map, Coords target)
    {
        if (target.X < 0 || target.Y < 0 || target.X >= map.Data.Width || target.Y >= map.Data.Height)
        {
            return false;
        }

        if (!_mapTileService.IsTileWalkable(map.Data, target))
        {
            return false;
        }

        return !map.IsTileOccupied(target);
    }

    /// <summary>
    ///     Wrap-aware timestamp difference matching eoserv's
    ///     <c>Timestamp::operator-</c> and SpellCastService.TimestampDiff.
    /// </summary>
    private static int TimestampDiff(int a, int b)
    {
        if (a == -1)
        {
            return b;
        }

        if (b == -1)
        {
            return a;
        }

        return b > a ? a - b + MaxTimestamp : a - b;
    }

    private static bool TryGetTargetCoords(Character character, Direction direction, out Coords target)
    {
        target = new Coords { X = character.X, Y = character.Y };

        switch (direction)
        {
            case Direction.Left:
                target.X--;
                return true;
            case Direction.Right:
                target.X++;
                return true;
            case Direction.Up:
                target.Y--;
                return true;
            case Direction.Down:
                target.Y++;
                return true;
            default:
                return false;
        }
    }

    private async Task ClearInteractionsAsync(PlayerState player)
    {
        player.InteractingNpcIndex = null;
        player.InteractingBoardId = null;
        player.InteractingChestCoords = null;

        var trade = player.TradeSession;
        if (trade is null)
        {
            return;
        }

        player.TradeSession = null;
        player.PendingTradeRequestFromPlayerId = null;

        var partner = trade.Partner;
        if (partner.TradeSession is null)
        {
            return;
        }

        partner.TradeSession = null;
        partner.PendingTradeRequestFromPlayerId = null;

        await partner.Send(new TradeCloseServerPacket
        {
            PartnerPlayerId = player.SessionId
        });
    }

    private static MapWarpRowTile? TryGetWarpTile(MapState map, int x, int y)
    {
        return map.Data.WarpRows
            .Where(row => row.Y == y)
            .SelectMany(row => row.Tiles)
            .FirstOrDefault(tile => tile.X == x && tile.Warp is not null);
    }

    /// <summary>
    ///     A warp tile is only usable when the character meets its level
    ///     requirement and, for door warps, the door is currently open. Door keys
    ///     are not yet modelled.
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
}
