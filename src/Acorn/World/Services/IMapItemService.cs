using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services;

/// <summary>
/// Result of an item drop operation.
/// </summary>
public record ItemDropResult(bool Success, int? ItemIndex = null, string? ErrorMessage = null);

/// <summary>
/// Result of an item pickup operation.
/// </summary>
public record ItemPickupResult(bool Success, int ItemId = 0, int Amount = 0, string? ErrorMessage = null);

/// <summary>
/// Service responsible for managing items on maps (dropping and picking up).
/// </summary>
public interface IMapItemService
{
    /// <summary>
    /// Attempts to drop an item from a player's inventory onto the map.
    /// </summary>
    Task<ItemDropResult> TryDropItem(PlayerState player, MapState map, int itemId, int amount, Coords coords);

    /// <summary>
    /// Attempts to pick up an item from the map into a player's inventory.
    /// </summary>
    Task<ItemPickupResult> TryPickupItem(PlayerState player, MapState map, int itemIndex);
}
