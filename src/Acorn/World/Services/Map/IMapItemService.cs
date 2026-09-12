using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Service responsible for managing items on maps (dropping and picking up).
/// </summary>
public interface IMapItemService
{
    /// <summary>
    ///     Attempts to drop an item from a player's inventory onto the map.
    /// </summary>
    Task<ItemDropResult> TryDropItem(PlayerState player, MapState map, int itemId, int amount, Coords coords);

    /// <summary>
    ///     Attempts to pick up an item from the map into a player's inventory.
    /// </summary>
    Task<ItemPickupResult> TryPickupItem(PlayerState player, MapState map, int itemIndex);
}