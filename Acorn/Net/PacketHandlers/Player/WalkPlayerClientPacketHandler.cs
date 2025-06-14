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

    public async Task HandleAsync(PlayerConnection playerConnection,
        WalkPlayerClientPacket packet)
    {
        if (playerConnection.Character is null)
        {
            _logger.LogError(
                "Tried to handler player walk, but the character associated with this connection has not been initialised");
            return;
        }

        playerConnection.Character.X = packet.WalkAction.Direction switch
        {
            Direction.Left => playerConnection.Character.X - 1,
            Direction.Right => playerConnection.Character.X + 1,
            _ => playerConnection.Character.X
        };
        playerConnection.Character.Y = packet.WalkAction.Direction switch
        {
            Direction.Up => playerConnection.Character.Y - 1,
            Direction.Down => playerConnection.Character.Y + 1,
            _ => playerConnection.Character.Y
        };

        playerConnection.Character.Direction = packet.WalkAction.Direction;

        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            _logger.LogError("Tried to handle walk player packet, but the map for the player connection was not found. MapId: {MapId}, PlayerId: {PlayerId}",
                playerConnection.Character.Map, playerConnection.SessionId);
            return;
        }


        var hasWarp = TryGetWarpTile(map, playerConnection.Character, out var warpTile);

        if (hasWarp && warpTile is not null)
        {
            await _world.Warp(
                playerConnection, 
                warpTile.Warp.DestinationMap, 
                warpTile.Warp.DestinationCoords.X,
                warpTile.Warp.DestinationCoords.Y, 
                WarpEffect.None, 
                playerConnection.Character.Map == warpTile.Warp.DestinationMap);
        }
        else
        {
            await map.BroadcastPacket(new WalkPlayerServerPacket
            {
                Direction = playerConnection.Character.Direction,
                PlayerId = playerConnection.SessionId,
                Coords = new Coords
                {
                    X = playerConnection.Character.X,
                    Y = playerConnection.Character.Y
                }
            }, except: playerConnection);
        }
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

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (WalkPlayerClientPacket)packet);
    }
}