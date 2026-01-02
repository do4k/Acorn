using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRemoveClientPacketHandler : IPacketHandler<PaperdollRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, PaperdollRemoveClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            return;
        }

        var character = playerState.Character;

        // Get the item currently in the slot
        var equippedItemId = PaperdollUtilities.GetSlotValue(character.Paperdoll, packet.SubLoc);

        // Item must match and slot must not be empty
        if (equippedItemId != packet.ItemId || equippedItemId == 0)
        {
            return;
        }

        // Unequip the item
        UnequipItemFromSlot(character, packet.SubLoc);

        // Send remove response to player
        await playerState.Send(new PaperdollRemoveServerPacket
        {
            ItemId = packet.ItemId,
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

    private void UnequipItemFromSlot(Acorn.Game.Models.Character character, int subLoc)
    {
        PaperdollUtilities.SetSlotValue(character.Paperdoll, subLoc, 0);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollRemoveClientPacket)packet);
}

