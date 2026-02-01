using Acorn.Data;
using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopOpenClientPacketHandler(
    ILogger<ShopOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IShopDataRepository shopDataRepository)
    : IPacketHandler<ShopOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open shop without character or map", player.SessionId);
            return;
        }

        // Find the NPC by index on the map
        var npcIndex = packet.NpcIndex;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null)
        {
            logger.LogWarning("Player {Character} tried to open shop at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's a shop NPC
        if (npc.npc.Data.Type != NpcType.Shop)
        {
            logger.LogWarning("Player {Character} tried to open shop at non-shop NPC {NpcId}",
                player.Character.Name, npc.npc.Id);
            return;
        }

        // Get shop data by NPC's behavior ID
        var shop = shopDataRepository.GetShopByBehaviorId(npc.npc.Data.BehaviorId);
        if (shop == null)
        {
            logger.LogWarning("No shop data found for NPC behavior ID {BehaviorId}", npc.npc.Data.BehaviorId);
            return;
        }

        logger.LogInformation("Player {Character} opening shop {ShopName}",
            player.Character.Name, shop.Name);

        // Store the NPC index for subsequent buy/sell operations
        player.InteractingNpcIndex = npcIndex;

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
