using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Trade;

/// <summary>
/// Handles accepting a trade request - opens the trade window for both players
/// </summary>
public class TradeAcceptClientPacketHandler(
    ILogger<TradeAcceptClientPacketHandler> logger)
    : IPacketHandler<TradeAcceptClientPacket>
{
    private const int MaxTradeDistance = 11;

    public async Task HandleAsync(PlayerState player, TradeAcceptClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to accept trade without character or map", player.SessionId);
            return;
        }

        // Can't accept if already in a trade
        if (player.TradeSession != null)
        {
            logger.LogDebug("Player {Character} is already in a trade", player.Character.Name);
            return;
        }

        var partnerPlayerId = packet.PlayerId;

        // Find partner player who should have requested to trade with us
        var partnerPlayer = player.CurrentMap.Players.FirstOrDefault(p => p.SessionId == partnerPlayerId);
        if (partnerPlayer?.Character == null)
        {
            logger.LogDebug("Partner player {PartnerId} not found on map", partnerPlayerId);
            return;
        }

        // Verify the partner actually requested to trade with us
        if (partnerPlayer.PendingTradeRequestFromPlayerId != player.SessionId)
        {
            logger.LogDebug("Partner {Partner} did not request trade with us", partnerPlayer.Character.Name);
            return;
        }

        // Can't accept if partner is already in a trade
        if (partnerPlayer.TradeSession != null)
        {
            logger.LogDebug("Partner {Partner} is already trading", partnerPlayer.Character.Name);
            return;
        }

        // Check if players are still in range
        var distance = Math.Max(
            Math.Abs(player.Character.X - partnerPlayer.Character.X),
            Math.Abs(player.Character.Y - partnerPlayer.Character.Y));

        if (distance > MaxTradeDistance)
        {
            logger.LogDebug("Players too far apart for trade ({Distance})", distance);
            return;
        }

        logger.LogInformation("Player {Character} accepting trade with {Partner}",
            player.Character.Name, partnerPlayer.Character.Name);

        // Clear the pending request
        partnerPlayer.PendingTradeRequestFromPlayerId = null;

        // Create trade sessions for both players
        player.TradeSession = new TradeSession
        {
            PartnerId = partnerPlayer.SessionId,
            Partner = partnerPlayer
        };

        partnerPlayer.TradeSession = new TradeSession
        {
            PartnerId = player.SessionId,
            Partner = player
        };

        // Send trade open to both players
        await player.Send(new TradeOpenServerPacket
        {
            PartnerPlayerId = partnerPlayer.SessionId,
            PartnerPlayerName = partnerPlayer.Character.Name,
            YourPlayerId = player.SessionId,
            YourPlayerName = player.Character.Name
        });

        await partnerPlayer.Send(new TradeOpenServerPacket
        {
            PartnerPlayerId = player.SessionId,
            PartnerPlayerName = player.Character.Name,
            YourPlayerId = partnerPlayer.SessionId,
            YourPlayerName = partnerPlayer.Character.Name
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (TradeAcceptClientPacket)packet);
    }
}
