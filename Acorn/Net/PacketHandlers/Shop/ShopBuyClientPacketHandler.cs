using Acorn.Database.Repository;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopBuyClientPacketHandler(
    ILogger<ShopBuyClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ShopBuyClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopBuyClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to buy from shop without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} buying item {ItemId} x{Amount} from shop",
            player.Character.Name, packet.BuyItem.Id, packet.BuyItem.Amount);

        // TODO: Get shop data from map/world
        // TODO: Validate shop sells this item
        // TODO: Calculate total cost
        // TODO: Check if player has enough gold (check for gold item ID in inventory)
        // TODO: Check weight limit: player.Character.CanCarryWeight(eif, itemId, amount)
        // TODO: Remove gold: player.Character.RemoveItem(goldItemId, totalCost)
        // TODO: Add purchased item: player.Character.AddItem(packet.BuyItem.Id, packet.BuyItem.Amount)
        // TODO: Send ShopBuy server packet with updated inventory and gold
        // TODO: Update character in database: await characterRepository.UpdateAsync(player.Character.AsDatabaseModel());

        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ShopBuyClientPacket)packet);
    }
}
