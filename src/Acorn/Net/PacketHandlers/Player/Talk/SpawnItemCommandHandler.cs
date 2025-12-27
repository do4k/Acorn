using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Net.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SpawnItemCommandHandler : ITalkHandler
{
    private readonly ILogger<SpawnItemCommandHandler> _logger;
    private readonly IDataFileRepository _dataFiles;
    private readonly INotificationService _notifications;
    private readonly IInventoryService _inventoryService;
    private readonly IDbRepository<Database.Models.Character> _characterRepository;
    private readonly ICharacterMapper _characterMapper;

    public SpawnItemCommandHandler(IDataFileRepository dataFiles,
        ILogger<SpawnItemCommandHandler> logger, INotificationService notifications,
        IInventoryService inventoryService,
        IDbRepository<Database.Models.Character> characterRepository,
        ICharacterMapper characterMapper)
    {
        _dataFiles = dataFiles;
        _logger = logger;
        _notifications = notifications;
        _inventoryService = inventoryService;
        _characterRepository = characterRepository;
        _characterMapper = characterMapper;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("spawnitem", StringComparison.InvariantCultureIgnoreCase)
               || command.Equals("sitem", StringComparison.InvariantCultureIgnoreCase)
               || command.Equals("si", StringComparison.InvariantCultureIgnoreCase);
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

        // Add item directly to inventory
        if (!_inventoryService.TryAddItem(playerState.Character, itemId, amount))
        {
            await _notifications.SystemMessage(playerState, "Failed to add item to inventory (full?).");
            return;
        }

        _logger.LogInformation("Admin {PlayerName} spawned item {ItemName} (ID: {ItemId}) x{Amount} to inventory",
            playerState.Character.Name, itemName, itemId, amount);

        // Save to database
        await _characterRepository.UpdateAsync(_characterMapper.ToDatabase(playerState.Character));

        await _notifications.SystemMessage(playerState, $"Added {itemName} x{amount} (ID: {itemId}) to inventory.");

        // Get current amount in inventory for the packet
        var inventoryItem = playerState.Character.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
        var currentAmount = inventoryItem?.Amount ?? amount;

        // Send inventory update to player
        await playerState.Send(new ItemObtainServerPacket
        {
            Item = new Moffat.EndlessOnline.SDK.Protocol.Net.ThreeItem
            {
                Id = itemId,
                Amount = currentAmount
            },
            CurrentWeight = 0 // Weight not dynamically tracked
        });
    }

    private async Task SpawnByName(PlayerState playerState, string name, int amount)
    {
        _logger.LogInformation("Searching for item with name: '{Name}', EIF has {Count} items", 
            name, _dataFiles.Eif.Items.Count);
        
        // Find exact match first
        var exactMatches = _dataFiles.Eif.FindByName(name);

        _logger.LogInformation("Found {ExactCount} exact matches", exactMatches.Count);

        if (exactMatches.Count == 1)
        {
            var (item, id) = exactMatches[0];
            await SpawnItem(playerState, id, item.Name, amount);
            return;
        }

        if (exactMatches.Count > 1)
        {
            var ids = string.Join(", ", exactMatches.Select(x => x.Id));
            await _notifications.SystemMessage(playerState, $"Multiple items found with name \"{name}\": IDs {ids}");
            return;
        }

        // No exact match, try partial/contains match
        var partialMatches = _dataFiles.Eif.SearchByName(name);

        _logger.LogInformation("Found {PartialCount} partial matches", partialMatches.Count);

        if (partialMatches.Count == 1)
        {
            var (item, id) = partialMatches[0];
            await SpawnItem(playerState, id, item.Name, amount);
            return;
        }

        if (partialMatches.Count > 1)
        {
            var suggestions = string.Join(", ", partialMatches.Take(5).Select(x => $"{x.Item.Name} ({x.Id})"));
            await _notifications.SystemMessage(playerState, $"Multiple items match \"{name}\": {suggestions}");
            return;
        }

        await _notifications.SystemMessage(playerState, $"Item \"{name}\" not found.");
    }
}
