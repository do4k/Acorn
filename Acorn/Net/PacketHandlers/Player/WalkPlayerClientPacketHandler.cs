using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WalkPlayerClientPacketHandler : IPacketHandler<WalkPlayerClientPacket>
{
    private readonly ILogger<WalkPlayerClientPacketHandler> _logger;
    private readonly WorldState _world;

    public WalkPlayerClientPacketHandler(ILogger<WalkPlayerClientPacketHandler> logger, WorldState world)
    {
        _logger = logger;
        _world = world;
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

        if (playerState.CurrentMap is null)
        {
            _logger.LogError("Tried to handle walk player packet, but the map for the player connection was not found. MapId: {MapId}, PlayerId: {PlayerId}",
                playerState.Character.Map, playerState.SessionId);
            return;
        }

        await playerState.CurrentMap.BroadcastPacket(new WalkPlayerServerPacket
        {
            Direction = playerState.Character.Direction,
            PlayerId = playerState.SessionId,
            Coords = new Coords
            {
                X = playerState.Character.X,
                Y = playerState.Character.Y
            }
        }, except: playerState);

        var hasWarp = TryGetWarpTile(playerState.CurrentMap, playerState.Character, out var warpTile);
        if (hasWarp is false || warpTile is null)
        {
            return;
        }

        var targetMap = _world.MapForId(warpTile.Warp.DestinationMap);
        if (targetMap is null)
        {
            return;
        }

        await playerState.Warp(
            targetMap,
            warpTile.Warp.DestinationCoords.X,
            warpTile.Warp.DestinationCoords.Y);
    }

    private bool TryGetWarpTile(MapState map, Database.Models.Character character, out MapWarpRowTile? tile)
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

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (WalkPlayerClientPacket)packet);
    }
}