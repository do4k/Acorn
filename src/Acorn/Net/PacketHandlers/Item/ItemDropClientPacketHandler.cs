using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemDropClientPacketHandler(
    ILogger<ItemDropClientPacketHandler> logger,
    IMapItemService mapItemService,
    ICharacterMapper characterMapper,
    IWeightCalculator weightCalculator,
    IInventoryService inventoryService,
    IDataFileRepository dataFileRepository,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemDropClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemDropClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to drop item without character or map", player.SessionId);
            return;
        }

        // Convert ByteCoords to Coords (ByteCoords are encoded with +1 offset)
        var coords = new Coords { X = packet.Coords.X - 1, Y = packet.Coords.Y - 1 };

        // Use map item service for drop logic
        var result =
            await mapItemService.TryDropItem(player, player.CurrentMap, packet.Item.Id, packet.Item.Amount, coords);

        if (result.Success && result.ItemIndex.HasValue)
        {
            // Calculate remaining amount in inventory
            var remaining = inventoryService.GetItemAmount(player.Character, packet.Item.Id);

            // Calculate current weight
            var currentWeight = weightCalculator.GetCurrentWeight(player.Character, dataFileRepository.Eif);
            var maxWeight = player.Character.MaxWeight;

            // Send ItemDropServerPacket to confirm drop
            await player.Send(new ItemDropServerPacket
            {
                ItemIndex = result.ItemIndex.Value,
                DroppedItem = new ThreeItem
                {
                    Id = packet.Item.Id,
                    Amount = packet.Item.Amount
                },
                RemainingAmount = remaining,
                Coords = coords,
                Weight = new Weight
                {
                    Current = currentWeight,
                    Max = maxWeight
                }
            });

            logger.LogInformation("Player {Character} dropped item {ItemId} x{Amount} at ({X}, {Y})",
                player.Character.Name, packet.Item.Id, packet.Item.Amount, coords.X, coords.Y);

            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
        else
        {
            logger.LogWarning("Player {Character} failed to drop item: {Error}",
                player.Character.Name, result.ErrorMessage);
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ItemDropClientPacket)packet);
    }
}