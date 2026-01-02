using Acorn.Game.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRemoveClientPacketHandler : IPacketHandler<PaperdollRemoveClientPacket>
{
    private readonly IInventoryService _inventoryService;

    public PaperdollRemoveClientPacketHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }
    public async Task HandleAsync(PlayerState playerState, PaperdollRemoveClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            return;
        }

        var character = playerState.Character;

        // Get the item currently in the slot
        var equippedItemId = packet.SubLoc switch
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

        // Item must match and slot must not be empty
        if (equippedItemId != packet.ItemId || equippedItemId == 0)
        {
            return;
        }

        // Unequip the item
        UnequipItemFromSlot(character, packet.SubLoc);
        
        // Return the item to inventory
        _inventoryService.TryAddItem(character, packet.ItemId, 1);

        // Send remove response to player
        await playerState.Send(new PaperdollRemoveServerPacket
        {
            ItemId = packet.ItemId,
            SubLoc = packet.SubLoc
        });

        // Broadcast avatar change to nearby players (including self)
        var nearbyPlayers = playerState.CurrentMap.Players
            .Where(p => p.SessionId != playerState.SessionId)
            .ToList();

        if (nearbyPlayers.Count > 0)
        {
            var avatarChangePacket = new AvatarAgreeServerPacket();
            // Note: AvatarAgreeServerPacket structure needs to be populated with:
            // - PlayerId of the unequipping character
            // - Updated equipment data showing the removed item
            
            var broadcastTasks = nearbyPlayers.Select(p => p.Send(avatarChangePacket)).ToList();
            await Task.WhenAll(broadcastTasks);
        }
    }

    private void UnequipItemFromSlot(Acorn.Game.Models.Character character, int subLoc)
    {
        switch (subLoc)
        {
            case 1:
                character.Paperdoll.Hat = 0;
                break;
            case 2:
                character.Paperdoll.Necklace = 0;
                break;
            case 3:
                character.Paperdoll.Armor = 0;
                break;
            case 4:
                character.Paperdoll.Belt = 0;
                break;
            case 5:
                character.Paperdoll.Boots = 0;
                break;
            case 6:
                character.Paperdoll.Gloves = 0;
                break;
            case 7:
                character.Paperdoll.Weapon = 0;
                break;
            case 8:
                character.Paperdoll.Shield = 0;
                break;
            case 9:
                character.Paperdoll.Accessory = 0;
                break;
            case 10:
                character.Paperdoll.Ring1 = 0;
                break;
            case 11:
                character.Paperdoll.Ring2 = 0;
                break;
            case 12:
                character.Paperdoll.Bracer1 = 0;
                break;
            case 13:
                character.Paperdoll.Bracer2 = 0;
                break;
            case 14:
                character.Paperdoll.Armlet1 = 0;
                break;
            case 15:
                character.Paperdoll.Armlet2 = 0;
                break;
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollRemoveClientPacket)packet);
}

