using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemJunkClientPacketHandler(
    ILogger<ItemJunkClientPacketHandler> logger,
    IInventoryService inventoryService,
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
            logger.LogInformation("Player {Character} junked item {ItemId} x{Amount}",
                player.Character.Name, packet.Item.Id, packet.Item.Amount);

            // TODO: Send ItemJunk packet to player with updated inventory
            // await player.Send(new ItemJunkServerPacket { ... });

            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemJunkClientPacket)packet);
    }
}
