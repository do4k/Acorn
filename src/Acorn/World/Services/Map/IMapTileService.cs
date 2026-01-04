using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.World.Services.Map;

/// <summary>
///     Service for querying map tile information and spatial calculations.
/// </summary>
public interface IMapTileService
{
    /// <summary>
    ///     Get the tile spec at the given coordinates.
    /// </summary>
    MapTileSpec? GetTile(Emf map, Coords coords);

    /// <summary>
    ///     Check if a tile is walkable for NPCs.
    /// </summary>
    bool IsNpcWalkable(MapTileSpec tileSpec);

    /// <summary>
    ///     Check if the tile at the given coordinates is walkable.
    /// </summary>
    bool IsTileWalkable(Emf map, Coords coords);

    /// <summary>
    ///     Calculate Manhattan distance between two coordinates.
    /// </summary>
    int GetDistance(Coords a, Coords b);

    /// <summary>
    ///     Check if two coordinates are within client render range.
    /// </summary>
    bool InClientRange(Coords a, Coords b);

    /// <summary>
    ///     Check if a player is within range of a specific tile type.
    /// </summary>
    bool PlayerInRangeOfTile(Emf map, Coords playerCoords, MapTileSpec tileSpec);
}