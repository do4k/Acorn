using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.World.Services.Map;

public class MapTileService : IMapTileService
{
    private const int CLIENT_RANGE = 13;

    private static readonly HashSet<MapTileSpec> NonWalkableTiles = new()
    {
        MapTileSpec.Wall,
        MapTileSpec.ChairDown,
        MapTileSpec.ChairLeft,
        MapTileSpec.ChairRight,
        MapTileSpec.ChairUp,
        MapTileSpec.ChairDownRight,
        MapTileSpec.ChairUpLeft,
        MapTileSpec.ChairAll,
        MapTileSpec.Chest,
        MapTileSpec.BankVault,
        MapTileSpec.Edge,
        MapTileSpec.Board1,
        MapTileSpec.Board2,
        MapTileSpec.Board3,
        MapTileSpec.Board4,
        MapTileSpec.Board5,
        MapTileSpec.Board6,
        MapTileSpec.Board7,
        MapTileSpec.Board8,
        MapTileSpec.Jukebox,
        MapTileSpec.NpcBoundary
    };

    public MapTileSpec? GetTile(Emf map, Coords coords)
    {
        var row = map.TileSpecRows.FirstOrDefault(r => r.Y == coords.Y);
        var tile = row?.Tiles.FirstOrDefault(t => t.X == coords.X);
        return tile?.TileSpec;
    }

    public bool IsNpcWalkable(MapTileSpec tileSpec)
        => !NonWalkableTiles.Contains(tileSpec);

    public bool IsTileWalkable(Emf map, Coords coords)
    {
        var tile = GetTile(map, coords);
        if (tile == null) return true;
        return IsNpcWalkable(tile.Value);
    }

    public int GetDistance(Coords a, Coords b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    public bool InClientRange(Coords a, Coords b)
        => GetDistance(a, b) <= CLIENT_RANGE;

    public bool PlayerInRangeOfTile(Emf map, Coords playerCoords, MapTileSpec tileSpec)
    {
        foreach (var row in map.TileSpecRows)
        {
            foreach (var tile in row.Tiles.Where(t => t.TileSpec == tileSpec))
            {
                var tileCoords = new Coords { X = tile.X, Y = row.Y };
                if (InClientRange(playerCoords, tileCoords))
                    return true;
            }
        }

        return false;
    }
}
