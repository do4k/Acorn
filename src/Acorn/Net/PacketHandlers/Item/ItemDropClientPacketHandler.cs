using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.World.Services.Map;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Item;

[RequiresCharacter]
public class ItemDropClientPacketHandler(
    ILogger<ItemDropClientPacketHandler> logger,
    IMapItemService mapItemService,
    ICharacterMapper characterMapper,
    IWeightCalculator weightCalculator,
    IInventoryService inventoryService,
    IDataFileRepository dataFileRepository,
    IDbRepository<Database.Models.Character> characterRepository,
    IOptions<ServerOptions> serverOptions,
    IQuestService questService,
    AcornMetrics metrics)
    : IPacketHandler<ItemDropClientPacket>
{
    private const int DropAtPlayerSentinel = 255;

    public async Task HandleAsync(PlayerState player, ItemDropClientPacket packet)
    {
        // Block dropping items while trading (mirrors eoserv Item.cpp)
        if (player.IsTrading)
        {
            return;
        }

        // eoserv: 255/255 means "drop at the player's current tile" instead of a target tile.
        var dropAtPlayer = packet.Coords.X == DropAtPlayerSentinel && packet.Coords.Y == DropAtPlayerSentinel;

        // ByteCoords are encoded with a +1 offset, so subtract one (eoserv's Number()).
        var coords = dropAtPlayer
            ? player.Character!.AsCoords()
            : new Coords { X = packet.Coords.X - 1, Y = packet.Coords.Y - 1 };

        var itemData = dataFileRepository.Eif.GetItem(packet.Item.Id);
        if (itemData is null)
        {
            logger.LogWarning("Player {Character} tried to drop unknown item {ItemId}",
                player.Character!.Name, packet.Item.Id);
            return;
        }

        // Lore items can never be dropped (matches eoserv).
        if (itemData.Special == ItemSpecial.Lore)
        {
            logger.LogWarning("Player {Character} tried to drop lore item {ItemId}",
                player.Character!.Name, packet.Item.Id);
            return;
        }

        // Clamp the requested amount to the configured maximum (matches eoserv's MaxDrop).
        var amount = packet.Item.Amount;
        var maxDrop = serverOptions.Value.MaxDrop;
        if (maxDrop > 0 && amount > maxDrop)
        {
            amount = maxDrop;
        }

        if (amount <= 0)
        {
            return;
        }

        // Jailed players cannot drop items (matches eoserv's JailMap check).
        if (player.IsJailed)
        {
            logger.LogWarning("Player {Character} tried to drop item {ItemId} while jailed",
                player.Character!.Name, packet.Item.Id);
            return;
        }

        // Use map item service for drop logic
        var result = await mapItemService.TryDropItem(player, player.CurrentMap!, packet.Item.Id, amount, coords);

        if (result.Success && result.ItemIndex.HasValue)
        {
            // Calculate remaining amount in inventory
            var remaining = inventoryService.GetItemAmount(player.Character!, packet.Item.Id);

            // Calculate current weight
            var currentWeight = weightCalculator.GetCurrentWeight(player.Character!, dataFileRepository.Eif);
            var maxWeight = player.Character!.MaxWeight;

            // Send ItemDropServerPacket to confirm drop
            await player.Send(new ItemDropServerPacket
            {
                ItemIndex = result.ItemIndex.Value,
                DroppedItem = new ThreeItem
                {
                    Id = packet.Item.Id,
                    Amount = amount
                },
                RemainingAmount = remaining,
                Coords = coords,
                Weight = new Weight
                {
                    Current = currentWeight,
                    Max = maxWeight
                }
            });

            metrics.ItemsDropped.Add(1,
                new("item_id", packet.Item.Id),
                new("source", "player"));

            logger.LogInformation("Player {Character} dropped item {ItemId} x{Amount} at ({X}, {Y})",
                player.Character!.Name, packet.Item.Id, amount, coords.X, coords.Y);

            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character!));

            // Quest rules may now be satisfied (e.g. LostItems)
            await questService.CheckQuestRules(player);
        }
        else
        {
            logger.LogWarning("Player {Character} failed to drop item: {Error}",
                player.Character!.Name, result.ErrorMessage);
        }
    }

}