using Acorn.Database.Repository;
using Acorn.Net.Services;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SpawnItemCommandHandler : ITalkHandler
{
    private readonly ILogger<SpawnItemCommandHandler> _logger;
    private readonly IDataFileRepository _dataFiles;
    private readonly INotificationService _notifications;

    public SpawnItemCommandHandler(IDataFileRepository dataFiles,
        ILogger<SpawnItemCommandHandler> logger, INotificationService notifications)
    {
        _dataFiles = dataFiles;
        _logger = logger;
        _notifications = notifications;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("spawnitem", StringComparison.InvariantCultureIgnoreCase)
               || command.Equals("sitem", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await _notifications.SystemMessage(playerState, "Usage: $[spawnitem | sitem] <item_id|item_name> [amount]");
            return;
        }

        // Parse amount if provided (last arg that's a number)
        int amount = 1;
        string[] nameArgs = args;

        if (args.Length > 1 && int.TryParse(args[^1], out var parsedAmount))
        {
            amount = Math.Max(1, parsedAmount);
            nameArgs = args[..^1];
        }

        // Try to parse as ID first
        if (int.TryParse(nameArgs[0], out var itemId))
        {
            var item = _dataFiles.Eif.GetItem(itemId);
            if (item is null)
            {
                await _notifications.SystemMessage(playerState, $"Item with ID {itemId} not found.");
                return;
            }

            await SpawnItem(playerState, itemId, item.Name, amount);
            return;
        }

        // Otherwise search by name
        var searchName = string.Join(" ", nameArgs);
        await SpawnByName(playerState, searchName, amount);
    }

    private async Task SpawnItem(PlayerState playerState, int itemId, string itemName, int amount)
    {
        if (playerState.Character is null)
        {
            _logger.LogError("Character has not been initialised on connection");
            return;
        }

        if (playerState.CurrentMap is null)
        {
            return;
        }

        var coords = new Coords
        {
            X = playerState.Character.X,
            Y = playerState.Character.Y
        };

        // Get next available item index
        var itemIndex = GetNextItemIndex(playerState.CurrentMap);
        
        // Create the map item
        var mapItem = new MapItem
        {
            Id = itemId,
            Amount = amount,
            Coords = coords,
            OwnerId = 0, // No owner protection for spawned items
            ProtectedTicks = 0
        };

        playerState.CurrentMap.Items[itemIndex] = mapItem;

        _logger.LogInformation("Admin {PlayerName} spawned item {ItemName} (ID: {ItemId}) x{Amount} at ({X}, {Y})",
            playerState.Character.Name, itemName, itemId, amount, coords.X, coords.Y);

        await _notifications.SystemMessage(playerState, $"Spawned {itemName} x{amount} (ID: {itemId}).");

        // Broadcast item appearance to all players on map
        await playerState.CurrentMap.BroadcastPacket(new ItemAddServerPacket
        {
            ItemId = itemId,
            ItemIndex = itemIndex,
            ItemAmount = amount,
            Coords = coords
        });
    }

    private async Task SpawnByName(PlayerState playerState, string name, int amount)
    {
        // Find exact match first
        var exactMatches = _dataFiles.Eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => x.item.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (exactMatches.Count == 1)
        {
            var (item, id) = exactMatches[0];
            await SpawnItem(playerState, id, item.Name, amount);
            return;
        }

        if (exactMatches.Count > 1)
        {
            var ids = string.Join(", ", exactMatches.Select(x => x.id));
            await _notifications.SystemMessage(playerState, $"Multiple items found with name \"{name}\": IDs {ids}");
            return;
        }

        // No exact match, try partial/contains match
        var partialMatches = _dataFiles.Eif.Items
            .Select((item, index) => (item, id: index + 1))
            .Where(x => x.item.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (partialMatches.Count == 1)
        {
            var (item, id) = partialMatches[0];
            await SpawnItem(playerState, id, item.Name, amount);
            return;
        }

        if (partialMatches.Count > 1)
        {
            var suggestions = string.Join(", ", partialMatches.Take(5).Select(x => $"{x.item.Name} ({x.id})"));
            await _notifications.SystemMessage(playerState, $"Multiple items match \"{name}\": {suggestions}");
            return;
        }

        await _notifications.SystemMessage(playerState, $"Item \"{name}\" not found.");
    }

    private static int GetNextItemIndex(MapState map, int seed = 1)
    {
        if (map.Items.ContainsKey(seed))
            return GetNextItemIndex(map, seed + 1);
        return seed;
    }
}
