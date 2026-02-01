using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles when a player closes/cancels the trade
/// </summary>
public class TradeCloseClientPacketHandler(
    ILogger<TradeCloseClientPacketHandler> logger)
    : IPacketHandler<TradeCloseClientPacket>
{
    public async Task HandleAsync(PlayerState player, TradeCloseClientPacket packet)
    {
        if (player.Character == null)
        {
            logger.LogWarning("Player {SessionId} attempted to close trade without character", player.SessionId);
            return;
        }

        var trade = player.TradeSession;
        if (trade == null)
        {
            // Also clear any pending trade request
            player.PendingTradeRequestFromPlayerId = null;
            logger.LogDebug("Player {Character} is not in a trade", player.Character.Name);
            return;
        }

        var partner = trade.Partner;
        var partnerTrade = partner.TradeSession;

        logger.LogInformation("Player {Character} cancelled trade with {Partner}",
            player.Character.Name, partner.Character?.Name ?? "unknown");

        // Clear trade sessions
        player.TradeSession = null;
        player.PendingTradeRequestFromPlayerId = null;

        if (partnerTrade != null)
        {
            partner.TradeSession = null;
            partner.PendingTradeRequestFromPlayerId = null;

            // Notify partner that trade was cancelled
            await partner.Send(new TradeCloseServerPacket
            {
                PartnerPlayerId = player.SessionId
            });
        }
    }

}
