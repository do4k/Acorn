using Acorn.Data;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Shop;

[RequiresCharacter]
public class ShopOpenClientPacketHandler(
    ILogger<ShopOpenClientPacketHandler> logger,
    IShopDataRepository shopDataRepository)
    : IPacketHandler<ShopOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopOpenClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateAndStartInteraction(player, packet.NpcIndex, NpcType.Shop, logger);
        if (npc is null) return;

        // Get shop data by NPC's behavior ID
        var shop = shopDataRepository.GetShopByBehaviorId(npc.Data.BehaviorId);
        if (shop == null)
        {
            logger.LogWarning("No shop data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        logger.LogInformation("Player {Character} opening shop {ShopName}",
            player.Character.Name, shop.Name);

        // Build trade items list
        var tradeItems = shop.Trades.Select(t => new Moffat.EndlessOnline.SDK.Protocol.Net.Server.ShopTradeItem
        {
            ItemId = t.ItemId,
            BuyPrice = t.BuyPrice,
            SellPrice = t.SellPrice,
            MaxBuyAmount = t.MaxAmount
        }).ToList();

        // Build craft items list
        var craftItems = shop.Crafts.Select(c =>
        {
            // Pad ingredients to exactly 4
            var ingredients = c.Ingredients.Take(4).ToList();
            while (ingredients.Count < 4)
            {
                ingredients.Add(new ShopCraftIngredient(0, 0));
            }

            return new Moffat.EndlessOnline.SDK.Protocol.Net.Server.ShopCraftItem
            {
                ItemId = c.ItemId,
                Ingredients =
                [
                    new CharItem { Id = ingredients[0].ItemId, Amount = ingredients[0].Amount },
                    new CharItem { Id = ingredients[1].ItemId, Amount = ingredients[1].Amount },
                    new CharItem { Id = ingredients[2].ItemId, Amount = ingredients[2].Amount },
                    new CharItem { Id = ingredients[3].ItemId, Amount = ingredients[3].Amount }
                ]
            };
        }).ToList();

        await player.Send(new ShopOpenServerPacket
        {
            SessionId = player.SessionId,
            ShopName = shop.Name,
            TradeItems = tradeItems,
            CraftItems = craftItems
        });
    }

}
