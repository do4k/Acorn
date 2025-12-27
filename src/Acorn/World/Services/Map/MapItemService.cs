using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Map;

/// <summary>
/// Default implementation of map item operations.
/// </summary>
public class MapItemService : IMapItemService
{
    private readonly IInventoryService _inventoryService;
    private readonly IWeightCalculator _weightCalculator;
    private readonly IDataFileRepository _dataRepository;
    private readonly IMapTileService _tileService;
    private readonly ILogger<MapItemService> _logger;

    private const int DropDistance = 2;
    private const int DropProtectTicks = 300; // ~3 seconds at 10 ticks/sec

    public MapItemService(
        IInventoryService inventoryService,
        IWeightCalculator weightCalculator,
        IDataFileRepository dataRepository,
        IMapTileService tileService,
        ILogger<MapItemService> logger)
    {
        _inventoryService = inventoryService;
        _weightCalculator = weightCalculator;
        _dataRepository = dataRepository;
        _tileService = tileService;
        _logger = logger;
    }

    public async Task<ItemDropResult> TryDropItem(PlayerState player, MapState map, int itemId, int amount, Coords coords)
    {
        if (player.Character == null)
            return new ItemDropResult(false, ErrorMessage: "No character");

        // Validate distance
        var playerCoords = player.Character.AsCoords();
        if (_tileService.GetDistance(playerCoords, coords) > DropDistance)
        {
            _logger.LogWarning("Player {Character} tried to drop item too far away", player.Character.Name);
            return new ItemDropResult(false, ErrorMessage: "Too far away");
        }

        // Validate amount
        if (amount <= 0 || !_inventoryService.HasItem(player.Character, itemId, amount))
        {
            _logger.LogWarning("Player {Character} tried to drop invalid amount of item {ItemId}",
                player.Character.Name, itemId);
            return new ItemDropResult(false, ErrorMessage: "Invalid amount");
        }

        // Validate tile is walkable
        if (!_tileService.IsTileWalkable(map.Data, coords))
        {
            _logger.LogWarning("Player {Character} tried to drop item on unwalkable tile", player.Character.Name);
            return new ItemDropResult(false, ErrorMessage: "Cannot drop here");
        }

        // Remove from player inventory
        if (!_inventoryService.TryRemoveItem(player.Character, itemId, amount))
            return new ItemDropResult(false, ErrorMessage: "Failed to remove from inventory");

        // Add to map
        var itemIndex = GetNextItemIndex(map);
        var mapItem = new MapItem
        {
            Id = itemId,
            Amount = amount,
            Coords = coords,
            OwnerId = player.SessionId,
            ProtectedTicks = DropProtectTicks
        };

        map.Items[itemIndex] = mapItem;

        _logger.LogInformation("Player {Character} dropped item {ItemId} x{Amount} at ({X}, {Y})",
            player.Character.Name, itemId, amount, coords.X, coords.Y);

        return new ItemDropResult(true, itemIndex);
    }

    public async Task<ItemPickupResult> TryPickupItem(PlayerState player, MapState map, int itemIndex)
    {
        if (player.Character == null)
            return new ItemPickupResult(false, ErrorMessage: "No character");

        // Check if item exists
        if (!map.Items.TryGetValue(itemIndex, out var mapItem))
        {
            _logger.LogWarning("Player {Character} tried to get non-existent item {ItemIndex}",
                player.Character.Name, itemIndex);
            return new ItemPickupResult(false, ErrorMessage: "Item not found");
        }

        // Check protection
        if (mapItem.ProtectedTicks > 0 && mapItem.OwnerId != player.SessionId)
        {
            _logger.LogWarning("Player {Character} tried to get protected item", player.Character.Name);
            return new ItemPickupResult(false, ErrorMessage: "Item is protected");
        }

        // Check distance
        var playerCoords = player.Character.AsCoords();
        if (_tileService.GetDistance(playerCoords, mapItem.Coords) > DropDistance)
        {
            _logger.LogWarning("Player {Character} tried to get item too far away", player.Character.Name);
            return new ItemPickupResult(false, ErrorMessage: "Too far away");
        }

        // Get item data for weight check
        var itemData = _dataRepository.Eif.GetItem(mapItem.Id);
        if (itemData == null)
        {
            _logger.LogError("Item {ItemId} not found in EIF", mapItem.Id);
            return new ItemPickupResult(false, ErrorMessage: "Item data not found");
        }

        // Check weight
        if (!_weightCalculator.CanCarry(player.Character, _dataRepository.Eif, mapItem.Id, mapItem.Amount))
        {
            _logger.LogWarning("Player {Character} cannot carry item weight", player.Character.Name);
            return new ItemPickupResult(false, ErrorMessage: "Too heavy");
        }

        // Add to inventory
        if (!_inventoryService.TryAddItem(player.Character, mapItem.Id, mapItem.Amount))
        {
            _logger.LogWarning("Player {Character} inventory full", player.Character.Name);
            return new ItemPickupResult(false, ErrorMessage: "Inventory full");
        }

        var pickedUpId = mapItem.Id;
        var pickedUpAmount = mapItem.Amount;

        // Remove from map
        map.Items.TryRemove(itemIndex, out _);

        // Broadcast removal
        await map.BroadcastPacket(new ItemRemoveServerPacket
        {
            ItemIndex = itemIndex
        });

        _logger.LogInformation("Player {Character} picked up item {ItemId} x{Amount}",
            player.Character.Name, pickedUpId, pickedUpAmount);

        return new ItemPickupResult(true, pickedUpId, pickedUpAmount);
    }

    private static int GetNextItemIndex(MapState map, int seed = 1)
    {
        if (map.Items.ContainsKey(seed))
            return GetNextItemIndex(map, seed + 1);
        return seed;
    }
}
