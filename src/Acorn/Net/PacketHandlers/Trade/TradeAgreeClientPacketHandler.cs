using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles when a player agrees/accepts the trade items.
/// If both players have agreed, the trade completes.
/// </summary>
public class TradeAgreeClientPacketHandler(
    ILogger<TradeAgreeClientPacketHandler> logger,
    IInventoryService inventoryService)
    : IPacketHandler<TradeAgreeClientPacket>
{
    private const int MaxItemAmount = 2000000000;

    public async Task HandleAsync(PlayerState player, TradeAgreeClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to agree trade without character or map", player.SessionId);
            return;
        }

        var trade = player.TradeSession;
        if (trade == null)
        {
            logger.LogDebug("Player {Character} is not in a trade", player.Character.Name);
            return;
        }

        var partner = trade.Partner;
        var partnerTrade = partner.TradeSession;
        if (partnerTrade == null || partner.Character == null)
        {
            logger.LogDebug("Partner not in trade anymore");
            return;
        }

        // Must have at least one item offered to agree
        if (!trade.MyItems.Any())
        {
            logger.LogDebug("Player {Character} tried to agree with no items", player.Character.Name);
            return;
        }

        // Partner must also have at least one item offered
        if (!partnerTrade.MyItems.Any())
        {
            logger.LogDebug("Partner {Partner} has no items offered", partner.Character.Name);
            return;
        }

        // Set this player as having agreed
        trade.IAccepted = true;

        logger.LogDebug("Player {Character} agreed to trade", player.Character.Name);

        // Check if both players have agreed
        if (partnerTrade.IAccepted)
        {
            // Complete the trade!
            await CompleteTrade(player, trade, partner, partnerTrade);
        }
        else
        {
            // Only this player has agreed - notify both parties
            await player.Send(new TradeSpecServerPacket { Agree = true });
            await partner.Send(new TradeAgreeServerPacket
            {
                Agree = true,
                PartnerPlayerId = player.SessionId
            });
        }
    }

    private async Task CompleteTrade(PlayerState player, Net.Models.TradeSession playerTrade,
        PlayerState partner, Net.Models.TradeSession partnerTrade)
    {
        logger.LogInformation("Trade completing between {Player} and {Partner}",
            player.Character!.Name, partner.Character!.Name);

        // Collect items to exchange
        var playerItems = playerTrade.MyItems.ToList();
        var partnerItems = partnerTrade.MyItems.ToList();

        // Remove items from player's inventory
        foreach (var item in playerItems)
        {
            inventoryService.TryRemoveItem(player.Character, item.ItemId, item.Amount);
        }

        // Remove items from partner's inventory
        foreach (var item in partnerItems)
        {
            inventoryService.TryRemoveItem(partner.Character, item.ItemId, item.Amount);
        }

        // Add partner's items to player
        foreach (var item in partnerItems)
        {
            var currentAmount = inventoryService.GetItemAmount(player.Character, item.ItemId);
            var canAdd = Math.Min(item.Amount, MaxItemAmount - currentAmount);
            if (canAdd > 0)
            {
                inventoryService.TryAddItem(player.Character, item.ItemId, canAdd);
            }
        }

        // Add player's items to partner
        foreach (var item in playerItems)
        {
            var currentAmount = inventoryService.GetItemAmount(partner.Character, item.ItemId);
            var canAdd = Math.Min(item.Amount, MaxItemAmount - currentAmount);
            if (canAdd > 0)
            {
                inventoryService.TryAddItem(partner.Character, item.ItemId, canAdd);
            }
        }

        // Build final trade data packets
        var playerTradeData = new List<TradeItemData>
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

        var partnerTradeData = new List<TradeItemData>
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

        // Clear trade sessions
        player.TradeSession = null;
        partner.TradeSession = null;

        // Send completion packets
        await player.Send(new TradeUseServerPacket
        {
            TradeData = playerTradeData
        });

        await partner.Send(new TradeUseServerPacket
        {
            TradeData = partnerTradeData
        });

        // Show trade emote to nearby players (optional)
        // Can broadcast an emote packet here if desired

        logger.LogInformation("Trade completed between {Player} and {Partner}",
            player.Character.Name, partner.Character.Name);
    }

}
