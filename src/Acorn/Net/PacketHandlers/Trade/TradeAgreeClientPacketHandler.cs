using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Quest;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles when a player agrees/accepts the trade items.
/// If both players have agreed, the trade completes.
/// </summary>
[RequiresCharacter]
public class TradeAgreeClientPacketHandler(
    ILogger<TradeAgreeClientPacketHandler> logger,
    ITradeService tradeService,
    IQuestService questService,
    AcornMetrics metrics)
    : IPacketHandler<TradeAgreeClientPacket>
{
    public async Task HandleAsync(PlayerState player, TradeAgreeClientPacket packet)
    {
        var trade = player.TradeSession;
        if (trade == null)
        {
            logger.LogDebug("Player {Character} is not in a trade", player.Character!.Name);
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
            logger.LogDebug("Player {Character} tried to agree with no items", player.Character!.Name);
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

        logger.LogDebug("Player {Character} agreed to trade", player.Character!.Name);

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

        // Snapshot the offers so they can be validated and swapped atomically.
        var playerItems = playerTrade.MyItems
            .Select(i => new TradeOffer(i.ItemId, i.Amount))
            .ToList();
        var partnerItems = partnerTrade.MyItems
            .Select(i => new TradeOffer(i.ItemId, i.Amount))
            .ToList();

        var result = tradeService.TryCompleteTrade(
            player.Character!, playerItems,
            partner.Character!, partnerItems);

        if (!result.Success)
        {
            // The trade is no longer valid (missing items / over weight). Abort it
            // without moving any items and reset both players' agreement.
            logger.LogWarning("Trade between {Player} and {Partner} aborted: {Reason}",
                player.Character!.Name, partner.Character!.Name, result.Status);

            playerTrade.IAccepted = false;
            partnerTrade.IAccepted = false;

            await player.Send(new TradeSpecServerPacket { Agree = false });
            await partner.Send(new TradeSpecServerPacket { Agree = false });
            return;
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

        // Show the trade emote to nearby players
        await BroadcastTradeEmote(player);
        await BroadcastTradeEmote(partner);

        // Re-evaluate quest rules now that inventories have changed
        await questService.CheckQuestRules(player);
        await questService.CheckQuestRules(partner);

        metrics.TradesCompleted.Add(1);

        logger.LogInformation("Trade completed between {Player} and {Partner}",
            player.Character!.Name, partner.Character.Name);
    }

    private static Task BroadcastTradeEmote(PlayerState player)
    {
        if (player.CurrentMap is null)
        {
            return Task.CompletedTask;
        }

        return player.CurrentMap.BroadcastPacket(new EmotePlayerServerPacket
        {
            PlayerId = player.SessionId,
            Emote = Emote.Trade
        }, player);
    }
}
