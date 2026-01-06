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

public class ItemGetClientPacketHandler(
    ILogger<ItemGetClientPacketHandler> logger,
    IMapItemService mapItemService,
    ICharacterMapper characterMapper,
    IWeightCalculator weightCalculator,
    IDataFileRepository dataFileRepository,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemGetClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemGetClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to get item without character or map", player.SessionId);
            return;
        }

        logger.LogDebug("Player {Character} attempting to pick up item at index {ItemIndex}",
            player.Character.Name, packet.ItemIndex);

        // Use map item service for pickup logic
        var result = await mapItemService.TryPickupItem(player, player.CurrentMap, packet.ItemIndex);

        if (result.Success)
        {
            // Calculate current weight
            var currentWeight = weightCalculator.GetCurrentWeight(player.Character, dataFileRepository.Eif);
            var maxWeight = player.Character.MaxWeight;

            // Send ItemGetServerPacket to confirm pickup
            await player.Send(new ItemGetServerPacket
            {
                TakenItemIndex = packet.ItemIndex,
                TakenItem = new ThreeItem
                {
                    Id = result.ItemId,
                    Amount = result.Amount
                },
                Weight = new Weight
                {
                    Current = currentWeight,
                    Max = maxWeight
                }
            });

            logger.LogInformation("Player {Character} picked up item {ItemId} x{Amount}",
                player.Character.Name, result.ItemId, result.Amount);

            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
        else
        {
            logger.LogWarning("Player {Character} failed to pick up item {ItemIndex}: {Error}",
                player.Character.Name, packet.ItemIndex, result.ErrorMessage);
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ItemGetClientPacket)packet);
    }
}