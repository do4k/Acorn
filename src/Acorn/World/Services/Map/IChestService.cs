using Acorn.Game.Models;
using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.World.Services.Map;

/// <summary>
///     Shared validation and lookup logic for map chests. Chest interaction is
///     limited to tiles orthogonally adjacent to the player (Manhattan distance &lt;= 1).
/// </summary>
public interface IChestService
{
    /// <summary>
    ///     Whether the coordinates fall inside the map bounds.
    /// </summary>
    bool IsInBounds(MapState map, Coords coords);

    /// <summary>
    ///     Whether the tile at the coordinates is a chest tile.
    /// </summary>
    bool IsChestTile(MapState map, Coords coords);

    /// <summary>
    ///     Whether the character is orthogonally adjacent to the coordinates.
    /// </summary>
    bool IsAdjacent(Character character, Coords coords);

    /// <summary>
    ///     Gets the chest state for the coordinates, creating it on first interaction.
    /// </summary>
    MapChest GetOrCreateChest(MapState map, Coords coords);

    /// <summary>
    ///     Returns players within adjacency of the chest (excluding <paramref name="except" />)
    ///     that should receive chest content updates.
    /// </summary>
    IReadOnlyList<PlayerState> GetNearbyObservers(MapState map, Coords coords, PlayerState? except = null);

    /// <summary>
    ///     Projects the chest contents into the wire format used by chest packets.
    /// </summary>
    List<ThreeItem> ToThreeItems(MapChest chest);
}
