using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Trade;

public class TradeRequestClientPacketHandler(
    ILogger<TradeRequestClientPacketHandler> logger)
    : IPacketHandler<TradeRequestClientPacket>
{
    private const int MaxTradeDistance = 11; // Client range

    public async Task HandleAsync(PlayerState player, TradeRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to trade without character or map", player.SessionId);
            return;
        }

        // Can't trade if already in a trade
        if (player.TradeSession != null)
        {
            logger.LogDebug("Player {Character} is already in a trade", player.Character.Name);
            return;
        }

        var targetPlayerId = packet.PlayerId;

        // Find target player on same map
        var targetPlayer = player.CurrentMap.Players.FirstOrDefault(p => p.SessionId == targetPlayerId);
        if (targetPlayer?.Character == null)
        {
            logger.LogDebug("Target player {TargetId} not found on map", targetPlayerId);
            return;
        }

        // Can't trade with yourself
        if (targetPlayer == player)
        {
            return;
        }

        // Can't trade with player already in a trade
        if (targetPlayer.TradeSession != null)
        {
            logger.LogDebug("Target player {Target} is already trading", targetPlayer.Character.Name);
            return;
        }

        // Check if players are in range
        var distance = Math.Max(
            Math.Abs(player.Character.X - targetPlayer.Character.X),
            Math.Abs(player.Character.Y - targetPlayer.Character.Y));

        if (distance > MaxTradeDistance)
        {
            logger.LogDebug("Players too far apart for trade ({Distance})", distance);
            return;
        }

        logger.LogInformation("Player {Character} requesting trade with {Target}",
            player.Character.Name, targetPlayer.Character.Name);

        // Store who we're requesting to trade with
        player.PendingTradeRequestFromPlayerId = targetPlayerId;

        // Send trade request to target player
        await targetPlayer.Send(new TradeRequestServerPacket
        {
            PartnerPlayerId = player.SessionId,
            PartnerPlayerName = player.Character.Name
        });
    }

}
