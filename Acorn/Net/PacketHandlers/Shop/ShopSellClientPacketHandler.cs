using Acorn.Database.Repository;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopSellClientPacketHandler(
    ILogger<ShopSellClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ShopSellClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopSellClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to sell to shop without character or map", player.SessionId);
            return;
        }

        // Validate player has the item
        if (!player.Character.HasItem(packet.SellItem.Id, packet.SellItem.Amount))
        {
            logger.LogWarning("Player {Character} tried to sell item {ItemId} x{Amount} but doesn't have it",
                player.Character.Name, packet.SellItem.Id, packet.SellItem.Amount);
            return;
        }

        logger.LogInformation("Player {Character} selling item {ItemId} x{Amount} to shop",
            player.Character.Name, packet.SellItem.Id, packet.SellItem.Amount);

        // TODO: Get shop data from map/world
        // TODO: Validate shop accepts this item
        // TODO: Calculate sell value
        // TODO: Remove sold item: player.Character.RemoveItem(packet.SellItem.Id, packet.SellItem.Amount)
        // TODO: Add gold to inventory: player.Character.AddItem(goldItemId, sellValue)
        // TODO: Send ShopSell server packet with updated inventory and gold
        // TODO: Update character in database: await characterRepository.UpdateAsync(player.Character.AsDatabaseModel());
        
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ShopSellClientPacket)packet);
    }
}
