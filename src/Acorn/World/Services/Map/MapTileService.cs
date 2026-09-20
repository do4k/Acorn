using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.World.Services.Map;

public class MapTileService : IMapTileService
{
    private const int CLIENT_VIEW_RANGE_UPPER = 11;
    private const int CLIENT_VIEW_RANGE_LOWER = 14;

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
        MapTileSpec.Jukebox
    };

    /// <summary>
    ///     Tiles that only constrain NPC movement. Players may walk over an NPC boundary,
    ///     matching eoserv's <c>Map_Tile::Walkable(npc)</c>, which blocks it for NPCs only.
    /// </summary>
    private static readonly HashSet<MapTileSpec> NpcOnlyNonWalkableTiles = new()
    {
        MapTileSpec.NpcBoundary
    };

    public MapTileSpec? GetTile(Emf map, Coords coords)
    {
        var row = map.TileSpecRows.FirstOrDefault(r => r.Y == coords.Y);
        var tile = row?.Tiles.FirstOrDefault(t => t.X == coords.X);
        return tile?.TileSpec;
    }

    public bool IsNpcWalkable(MapTileSpec tileSpec)
    {
        return !NonWalkableTiles.Contains(tileSpec) && !NpcOnlyNonWalkableTiles.Contains(tileSpec);
    }

    public bool IsTileWalkable(Emf map, Coords coords)
    {
        var tile = GetTile(map, coords);
        if (tile == null)
        {
            return true;
        }

        return !NonWalkableTiles.Contains(tile.Value);
    }

    public int GetDistance(Coords a, Coords b)
    {
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    public int GetManhattanDistance(Coords a, Coords b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    public bool InClientRange(Coords observer, Coords other)
    {
        var distance = Math.Abs(observer.X - other.X) + Math.Abs(observer.Y - other.Y);

        // Mirrors the client's own cull range: Manhattan distance, asymmetric. The native
        // client keeps entities up to 12 tiles away when they are above/left of the
        // observer and 15 otherwise; eoweb culls at 11/14. Send the smaller of the two so
        // a client never immediately discards an entity we just sent (which made far-away
        // NPCs flicker in and out).
        return observer.X >= other.X || observer.Y >= other.Y
            ? distance <= CLIENT_VIEW_RANGE_UPPER
            : distance <= CLIENT_VIEW_RANGE_LOWER;
    }

    public bool PlayerInRangeOfTile(Emf map, Coords playerCoords, MapTileSpec tileSpec)
    {
        foreach (var row in map.TileSpecRows)
        {
            foreach (var tile in row.Tiles.Where(t => t.TileSpec == tileSpec))
            {
                var tileCoords = new Coords { X = tile.X, Y = row.Y };
                if (InClientRange(playerCoords, tileCoords))
                {
                    return true;
                }
            }
        }

        return false;
    }
}