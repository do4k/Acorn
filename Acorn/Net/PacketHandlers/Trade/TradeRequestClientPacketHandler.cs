using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Trade;

public class TradeRequestClientPacketHandler(ILogger<TradeRequestClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<TradeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, TradeRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to trade without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} initiating trade with player {TargetPlayerId}",
            player.Character.Name, packet.PlayerId);

        // TODO: Implement map.InitiateTrade(player, targetPlayerId)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (TradeRequestClientPacket)packet);
    }
}
