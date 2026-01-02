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
        var targetSlotValue = PaperdollUtilities.GetSlotValue(character.Paperdoll, packet.SubLoc);

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
        PaperdollUtilities.SetSlotValue(character.Paperdoll, subLoc, itemId);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollAddClientPacket)packet);
}

