using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles removing an item from the trade
/// </summary>
public class TradeRemoveClientPacketHandler(
    ILogger<TradeRemoveClientPacketHandler> logger)
    : IPacketHandler<TradeRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, TradeRemoveClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to remove trade item without character or map", player.SessionId);
            return;
        }

        var trade = player.TradeSession;
        if (trade == null)
        {
            logger.LogDebug("Player {Character} is not in a trade", player.Character.Name);
            return;
        }

        var partnerTrade = trade.Partner.TradeSession;
        if (partnerTrade == null)
        {
            logger.LogDebug("Partner not in trade anymore");
            return;
        }

        var itemId = packet.ItemId;

        // Check if item exists in trade
        var existingItem = trade.MyItems.FirstOrDefault(i => i.ItemId == itemId);
        if (existingItem == null)
        {
            logger.LogDebug("Item {ItemId} not in trade", itemId);
            return;
        }

        // Remove item from trade
        trade.RemoveItem(itemId);

        // When items change, both players need to un-accept
        trade.IAccepted = false;
        partnerTrade.IAccepted = false;

        logger.LogDebug("Player {Character} removed item {ItemId} from trade",
            player.Character.Name, itemId);

        // Send trade update to both players
        await SendTradeUpdate(player, trade.Partner, trade, partnerTrade);
    }

    private async Task SendTradeUpdate(PlayerState player, PlayerState partner,
        Net.Models.TradeSession playerTrade, Net.Models.TradeSession partnerTrade)
    {
        // Build trade data for packets
        var tradeData = new List<TradeItemData>
        {
            new TradeItemData
            {
                PlayerId = partner.SessionId,
                Items = partnerTrade.GetItemsForPacket()
            },
            new TradeItemData
            {
                PlayerId = player.SessionId,
                Items = playerTrade.GetItemsForPacket()
            }
        };

        var reverseTradeData = new List<TradeItemData>
        {
            new TradeItemData
            {
                PlayerId = player.SessionId,
                Items = playerTrade.GetItemsForPacket()
            },
            new TradeItemData
            {
                PlayerId = partner.SessionId,
                Items = partnerTrade.GetItemsForPacket()
            }
        };

        // Send update to player
        await player.Send(new TradeReplyServerPacket
        {
            TradeData = tradeData
        });

        // Check if partner had accepted - if so, send Admin packet to un-accept
        if (partnerTrade.IAccepted)
        {
            partnerTrade.IAccepted = false;
            await partner.Send(new TradeAdminServerPacket
            {
                TradeData = reverseTradeData
            });
        }
        else
        {
            await partner.Send(new TradeReplyServerPacket
            {
                TradeData = reverseTradeData
            });
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (TradeRemoveClientPacket)packet);
    }
}
