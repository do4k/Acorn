using Acorn.Database.Repository;
using Acorn.Net.PacketHandlers;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Chest;

[RequiresCharacter]
public class ChestOpenClientPacketHandler(
    ILogger<ChestOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IChestService chestService)
    : IPacketHandler<ChestOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChestOpenClientPacket packet)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        var chestCoords = packet.Coords;
        var map = player.CurrentMap;

        if (!chestService.IsInBounds(map, chestCoords) || !chestService.IsChestTile(map, chestCoords))
        {
            logger.LogWarning("Player {Character} tried to open chest at non-chest tile ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        // Chests can only be interacted with from an orthogonally adjacent tile.
        if (!chestService.IsAdjacent(player.Character, chestCoords))
        {
            logger.LogWarning("Player {Character} tried to open chest at ({X}, {Y}) out of range",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        var chest = chestService.GetOrCreateChest(map, chestCoords);

        if (chest.RequiredKeyId.HasValue && !HasKey(player, chest.RequiredKeyId.Value))
        {
            logger.LogDebug("Player {Character} doesn't have key for chest", player.Character.Name);

            // Chest/Close is the locked-chest reply and carries the required key.
            await player.Send(new ChestCloseServerPacket { Key = chest.RequiredKeyId.Value });
            return;
        }

        logger.LogInformation("Player {Character} opening chest at ({X}, {Y})",
            player.Character.Name, chestCoords.X, chestCoords.Y);

        // Store chest coords for the duration of the interaction.
        player.InteractingChestCoords = chestCoords;

        await player.Send(new ChestOpenServerPacket
        {
            Coords = chestCoords,
            Items = chestService.ToThreeItems(chest)
        });
    }

    private bool HasKey(PlayerState player, int requiredKeyId)
    {
        return player.Character!.Inventory.Items.Any(item =>
        {
            var itemData = dataFileRepository.Eif.GetItem(item.Id);
            return itemData is { Type: ItemType.Key } && itemData.Spec1 == requiredKeyId;
        });
    }
}
