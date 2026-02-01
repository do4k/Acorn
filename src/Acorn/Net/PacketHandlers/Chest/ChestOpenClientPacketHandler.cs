using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Chest;

public class ChestOpenClientPacketHandler(
    ILogger<ChestOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IMapTileService mapTileService,
    IInventoryService inventoryService)
    : IPacketHandler<ChestOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChestOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open chest without character or map", player.SessionId);
            return;
        }

        var chestCoords = packet.Coords;
        var playerCoords = new Coords { X = player.Character.X, Y = player.Character.Y };

        // Check if chest tile exists at these coordinates
        var tile = mapTileService.GetTile(player.CurrentMap.Data, chestCoords);
        if (tile != MapTileSpec.Chest)
        {
            logger.LogWarning("Player {Character} tried to open chest at non-chest tile ({X}, {Y})",
                player.Character.Name, chestCoords.X, chestCoords.Y);
            return;
        }

        // Check if player is in range (adjacent)
        var distance = Math.Max(Math.Abs(playerCoords.X - chestCoords.X), Math.Abs(playerCoords.Y - chestCoords.Y));
        if (distance > 1)
        {
            logger.LogWarning("Player {Character} tried to open chest too far away",
                player.Character.Name);
            return;
        }

        // Get or create chest state
        var chest = player.CurrentMap.Chests.GetOrAdd(chestCoords, _ => new MapChest
        {
            Coords = chestCoords
        });

        // Check if chest requires a key
        if (chest.RequiredKeyId.HasValue)
        {
            var hasKey = player.Character.Inventory.Items.Any(item =>
            {
                var itemData = dataFileRepository.Eif.GetItem(item.Id);
                return itemData?.Type == ItemType.Key && itemData.Spec1 == chest.RequiredKeyId.Value;
            });

            if (!hasKey)
            {
                logger.LogDebug("Player {Character} doesn't have key for chest", player.Character.Name);
                return;
            }
        }

        logger.LogInformation("Player {Character} opening chest at ({X}, {Y})",
            player.Character.Name, chestCoords.X, chestCoords.Y);

        // Store chest coords for subsequent add/take operations
        player.InteractingChestCoords = chestCoords;

        // Build chest items list
        var chestItems = chest.Items.Select(item => new ThreeItem
        {
            Id = item.ItemId,
            Amount = item.Amount
        }).ToList();

        await player.Send(new ChestOpenServerPacket
        {
            Coords = chestCoords,
            Items = chestItems
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ChestOpenClientPacket)packet);
    }
}
