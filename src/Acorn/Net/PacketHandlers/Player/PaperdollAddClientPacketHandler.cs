using Acorn.Game.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollAddClientPacketHandler : IPacketHandler<PaperdollAddClientPacket>
{
    private readonly IInventoryService _inventoryService;

    public PaperdollAddClientPacketHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task HandleAsync(PlayerState playerState, PaperdollAddClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            return;
        }

        var character = playerState.Character;

        // Check if the player has the item
        if (!_inventoryService.HasItem(character, packet.ItemId))
        {
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

        // If equipping to an empty slot
        if (targetSlotValue == 0)
        {
            // Equip the item
            EquipItemToSlot(character, packet.ItemId, packet.SubLoc);

            // Send success response to player
            await playerState.Send(new PaperdollAgreeServerPacket
            {
                ItemId = packet.ItemId,
                RemainingAmount = _inventoryService.GetItemAmount(character, packet.ItemId),
                SubLoc = packet.SubLoc
            });

            // Broadcast avatar change to nearby players
            var nearbyPlayers = playerState.CurrentMap.Players
                .Where(p => p.SessionId != playerState.SessionId)
                .ToList();

            if (nearbyPlayers.Count > 0)
            {
                // TODO: Broadcast avatar/equipment change to nearby players
                // This would include sending updated equipment visibility data
            }
        }
        else
        {
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

