using Acorn.Extensions;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Microsoft.Extensions.Logging;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollAddClientPacketHandler : IPacketHandler<PaperdollAddClientPacket>
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<PaperdollAddClientPacketHandler> _logger;

    public PaperdollAddClientPacketHandler(IInventoryService inventoryService, ILogger<PaperdollAddClientPacketHandler> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState, PaperdollAddClientPacket packet)
    {
        _logger.LogDebug("PaperdollAdd received - ItemId: {ItemId}, SubLoc: {SubLoc}, Player: {Player}", 
            packet.ItemId, packet.SubLoc, playerState.Account?.Username);
        
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            _logger.LogWarning("PaperdollAdd failed - Character is null: {CharIsNull}, CurrentMap is null: {MapIsNull}", 
                playerState.Character is null, playerState.CurrentMap is null);
            return;
        }

        var character = playerState.Character;

        // Check if the player has the item
        var hasItem = _inventoryService.HasItem(character, packet.ItemId);
        _logger.LogDebug("Player {Player} has item {ItemId}: {HasItem}", 
            character.Name, packet.ItemId, hasItem);
        
        if (!hasItem)
        {
            _logger.LogWarning("Player {Player} attempted to equip item {ItemId} which they don't have", 
                character.Name, packet.ItemId);
            return;
        }

        // Get the current item in the target slot to check for swaps
        var targetSlotValue = packet.SubLoc switch
        {
            1 => character.Paperdoll.Hat,
            2 => character.Paperdoll.Necklace,
            3 => character.Paperdoll.Armor,
            4 => character.Paperdoll.Belt,
            5 => character.Paperdoll.Boots,
            6 => character.Paperdoll.Gloves,
            7 => character.Paperdoll.Weapon,
            8 => character.Paperdoll.Shield,
            9 => character.Paperdoll.Accessory,
            10 => character.Paperdoll.Ring1,
            11 => character.Paperdoll.Ring2,
            12 => character.Paperdoll.Bracer1,
            13 => character.Paperdoll.Bracer2,
            14 => character.Paperdoll.Armlet1,
            15 => character.Paperdoll.Armlet2,
            _ => 0
        };

        _logger.LogDebug("Target slot {SubLoc} current value: {CurrentItemId}", packet.SubLoc, targetSlotValue);

        // If equipping to an empty slot
        if (targetSlotValue == 0)
        {
            _logger.LogInformation("Equipping item {ItemId} to empty slot {SubLoc} for player {Player}", 
                packet.ItemId, packet.SubLoc, character.Name);
            
            // Equip the item
            EquipItemToSlot(character, packet.ItemId, packet.SubLoc);
            _logger.LogDebug("Item {ItemId} equipped to slot {SubLoc}", packet.ItemId, packet.SubLoc);
            
            // Remove the item from inventory
            var removeSuccess = _inventoryService.TryRemoveItem(character, packet.ItemId, 1);
            _logger.LogDebug("Removed item {ItemId} from inventory: {Success}", packet.ItemId, removeSuccess);

            var remainingAmount = _inventoryService.GetItemAmount(character, packet.ItemId);
            _logger.LogDebug("Remaining amount of item {ItemId}: {Amount}", packet.ItemId, remainingAmount);

            // Send success response to player
            _logger.LogDebug("Sending PaperdollAgreeServerPacket for item {ItemId}, remaining: {Remaining}", 
                packet.ItemId, remainingAmount);
            await playerState.Send(new PaperdollAgreeServerPacket
            {
                ItemId = packet.ItemId,
                RemainingAmount = remainingAmount,
                SubLoc = packet.SubLoc
            });

            // Broadcast avatar change to nearby players
            var nearbyPlayers = playerState.CurrentMap.Players
                .Where(p => p.SessionId != playerState.SessionId)
                .ToList();

            _logger.LogDebug("Broadcasting equipment change to {NearbyPlayerCount} nearby players", nearbyPlayers.Count);

            if (nearbyPlayers.Count > 0)
            {
                var avatarChangePacket = new AvatarAgreeServerPacket
                {
                    Change = new AvatarChange
                    {
                        PlayerId = playerState.SessionId,
                        ChangeType = AvatarChangeType.Equipment,
                        ChangeTypeData = new AvatarChange.ChangeTypeDataEquipment
                        {
                            Equipment = character.Equipment().AsEquipmentChange()
                        }
                    }
                };
                
                var broadcastTasks = nearbyPlayers.Select(p => p.Send(avatarChangePacket)).ToList();
                await Task.WhenAll(broadcastTasks);
                _logger.LogDebug("Equipment broadcast complete");
            }
        }
        else
        {
            _logger.LogWarning("Player {Player} attempted to equip to occupied slot {SubLoc} (current item: {CurrentItemId}) - swap not implemented", 
                character.Name, packet.SubLoc, targetSlotValue);
            // TODO: Implement swap logic - unequip the current item and equip the new one
            // This would involve sending PaperdollSwapServerPacket with the swapped item details
        }
    }

    private void EquipItemToSlot(Acorn.Game.Models.Character character, int itemId, int subLoc)
    {
        switch (subLoc)
        {
            case 1:
                character.Paperdoll.Hat = itemId;
                break;
            case 2:
                character.Paperdoll.Necklace = itemId;
                break;
            case 3:
                character.Paperdoll.Armor = itemId;
                break;
            case 4:
                character.Paperdoll.Belt = itemId;
                break;
            case 5:
                character.Paperdoll.Boots = itemId;
                break;
            case 6:
                character.Paperdoll.Gloves = itemId;
                break;
            case 7:
                character.Paperdoll.Weapon = itemId;
                break;
            case 8:
                character.Paperdoll.Shield = itemId;
                break;
            case 9:
                character.Paperdoll.Accessory = itemId;
                break;
            case 10:
                character.Paperdoll.Ring1 = itemId;
                break;
            case 11:
                character.Paperdoll.Ring2 = itemId;
                break;
            case 12:
                character.Paperdoll.Bracer1 = itemId;
                break;
            case 13:
                character.Paperdoll.Bracer2 = itemId;
                break;
            case 14:
                character.Paperdoll.Armlet1 = itemId;
                break;
            case 15:
                character.Paperdoll.Armlet2 = itemId;
                break;
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollAddClientPacket)packet);
}

