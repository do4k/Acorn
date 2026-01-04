using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Player;

/// <summary>
/// Service for handling player actions like warping, refreshing, movement, etc.
/// Separates game logic from network/session state.
/// </summary>
public interface IPlayerController
{
    /// <summary>
    /// Warp a player to a new location on a map.
    /// </summary>
    Task WarpAsync(PlayerState player, MapState targetMap, int x, int y, WarpEffect warpEffect = WarpEffect.None);

    /// <summary>
    /// Refresh a player's current map position.
    /// </summary>
    Task RefreshAsync(PlayerState player);

    /// <summary>
    /// Update player's position on the current map.
    /// </summary>
    Task MoveAsync(PlayerState player, int x, int y);

    /// <summary>
    /// Handle player facing a new direction.
    /// </summary>
    Task FaceAsync(PlayerState player, Moffat.EndlessOnline.SDK.Protocol.Direction direction);

    /// <summary>
    /// Sit a player on the ground.
    /// </summary>
    Task SitAsync(PlayerState player);

    /// <summary>
    /// Stand a player up from sitting.
    /// </summary>
    Task StandAsync(PlayerState player);

    /// <summary>
    /// Handle player death. Warps them to rescue spawn location.
    /// </summary>
    Task DieAsync(PlayerState player);

    /// <summary>
    /// Equip an item from the player's inventory to their paperdoll.
    /// Recalculates stats after successful equip.
    /// </summary>
    /// <param name="player">Player equipping the item</param>
    /// <param name="itemId">ID of item to equip</param>
    /// <param name="subLoc">Paperdoll slot (1-15)</param>
    /// <returns>True if equip was successful, false otherwise</returns>
    Task<bool> EquipItemAsync(PlayerState player, int itemId, int subLoc);

    /// <summary>
    /// Unequip an item from the player's paperdoll back to inventory.
    /// Recalculates stats after successful unequip.
    /// </summary>
    /// <param name="player">Player unequipping the item</param>
    /// <param name="itemId">ID of item to unequip</param>
    /// <param name="subLoc">Paperdoll slot (1-15)</param>
    /// <returns>True if unequip was successful, false otherwise</returns>
    Task<bool> UnequipItemAsync(PlayerState player, int itemId, int subLoc);
}
