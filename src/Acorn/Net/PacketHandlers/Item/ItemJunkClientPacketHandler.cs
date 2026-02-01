using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemJunkClientPacketHandler(
    ILogger<ItemJunkClientPacketHandler> logger,
    IInventoryService inventoryService,
    IWeightCalculator weightCalculator,
    IDataFileRepository dataFileRepository,
    ICharacterMapper characterMapper,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemJunkClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemJunkClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to junk item without character or map", player.SessionId);
            return;
        }

        // Validate player has the item
        if (!inventoryService.HasItem(player.Character, packet.Item.Id, packet.Item.Amount))
        {
            logger.LogWarning("Player {Character} tried to junk item {ItemId} x{Amount} but doesn't have it",
                player.Character.Name, packet.Item.Id, packet.Item.Amount);
            return;
        }

        // Remove from inventory (junking destroys the item)
        if (inventoryService.TryRemoveItem(player.Character, packet.Item.Id, packet.Item.Amount))
        {
            // Calculate remaining amount in inventory
            var remaining = inventoryService.GetItemAmount(player.Character, packet.Item.Id);

            // Calculate current weight
            var currentWeight = weightCalculator.GetCurrentWeight(player.Character, dataFileRepository.Eif);
            var maxWeight = player.Character.MaxWeight;

            // Send ItemJunkServerPacket to confirm junk
            await player.Send(new ItemJunkServerPacket
            {
                JunkedItem = new ThreeItem
                {
                    Id = packet.Item.Id,
                    Amount = packet.Item.Amount
                },
                RemainingAmount = remaining,
                Weight = new Weight
                {
                    Current = currentWeight,
                    Max = maxWeight
                }
            });

            logger.LogInformation("Player {Character} junked item {ItemId} x{Amount}",
                player.Character.Name, packet.Item.Id, packet.Item.Amount);

            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
    }

}