using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles adding an item to the trade
/// </summary>
public class TradeAddClientPacketHandler(
    ILogger<TradeAddClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<TradeAddClientPacket>
{
    private const int MaxTradeAmount = 2000000000;

    public async Task HandleAsync(PlayerState player, TradeAddClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to add trade item without character or map", player.SessionId);
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

        var itemId = packet.AddItem.Id;
        var amount = packet.AddItem.Amount;

        if (itemId <= 0 || amount <= 0 || amount > MaxTradeAmount)
        {
            return;
        }

        // Check if item is tradeable (not lore)
        var itemData = dataFileRepository.Eif.GetItem(itemId);
        if (itemData == null)
        {
            return;
        }

        if (itemData.Special == ItemSpecial.Lore)
        {
            logger.LogDebug("Player {Character} tried to trade lore item {ItemId}",
                player.Character.Name, itemId);
            return;
        }

        // Check if player has enough of the item
        var playerAmount = inventoryService.GetItemAmount(player.Character, itemId);
        if (playerAmount < amount)
        {
            amount = playerAmount;
        }

        if (amount <= 0)
        {
            return;
        }

        // Check if already offering this item - update amount
        // Or check if we have room for another item
        var existingItem = trade.MyItems.FirstOrDefault(i => i.ItemId == itemId);
        if (existingItem == null && trade.MyItems.Count >= TradeSession.MaxTradeSlots)
        {
            logger.LogDebug("Trade is full for player {Character}", player.Character.Name);
            return;
        }

        // Add or update item in trade
        trade.AddOrUpdateItem(itemId, amount);

        // When items change, both players need to un-accept
        trade.IAccepted = false;
        partnerTrade.IAccepted = false;

        logger.LogDebug("Player {Character} added {Amount}x item {ItemId} to trade",
            player.Character.Name, amount, itemId);

        // Send trade update to both players
        await SendTradeUpdate(player, trade.Partner, trade, partnerTrade);
    }

    private async Task SendTradeUpdate(PlayerState player, PlayerState partner,
        TradeSession playerTrade, TradeSession partnerTrade)
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
        return HandleAsync(playerState, (TradeAddClientPacket)packet);
    }
}
