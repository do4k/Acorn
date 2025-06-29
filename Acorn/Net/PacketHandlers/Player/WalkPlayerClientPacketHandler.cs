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

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        WalkPlayerClientPacket packet)
    {
        if (connectionHandler.CharacterController is null)
        {
            _logger.LogError(
                "Tried to handler player walk, but the character associated with this connection has not been initialised");
            return;
        }

        connectionHandler.CharacterController.Data.X = packet.WalkAction.Direction switch
        {
            Direction.Left => connectionHandler.CharacterController.Data.X - 1,
            Direction.Right => connectionHandler.CharacterController.Data.X + 1,
            _ => connectionHandler.CharacterController.Data.X
        };

        connectionHandler.CharacterController.Data.Y = packet.WalkAction.Direction switch
        {
            Direction.Up => connectionHandler.CharacterController.Data.Y - 1,
            Direction.Down => connectionHandler.CharacterController.Data.Y + 1,
            _ => connectionHandler.CharacterController.Data.Y
        };

        connectionHandler.CharacterController.Data.Direction = packet.WalkAction.Direction;

        if (connectionHandler.CurrentMap is null)
        {
            _logger.LogError("Tried to handle walk player packet, but the map for the player connection was not found. MapId: {MapId}, PlayerId: {PlayerId}",
                connectionHandler.CharacterController.Data.Map, connectionHandler.SessionId);
            return;
        }

        await connectionHandler.CurrentMap.BroadcastPacket(new WalkPlayerServerPacket
        {
            Direction = connectionHandler.CharacterController.Data.Direction,
            PlayerId = connectionHandler.SessionId,
            Coords = new Coords
            {
                X = connectionHandler.CharacterController.Data.X,
                Y = connectionHandler.CharacterController.Data.Y
            }
        }, except: connectionHandler);

        var hasWarp = TryGetWarpTile(connectionHandler.CurrentMap, connectionHandler.CharacterController.Data, out var warpTile);
        if (hasWarp is false || warpTile is null)
        {
            return;
        }

        var targetMap = _world.MapForId(warpTile.Warp.DestinationMap);
        if (targetMap is null)
        {
            return;
        }

        await connectionHandler.Warp(
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

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WalkPlayerClientPacket)packet);
    }
}