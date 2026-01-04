using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Marriage;

public class MarriageRequestClientPacketHandler(
    ILogger<MarriageRequestClientPacketHandler> logger,
    IWorldQueries worldQueries)
    : IPacketHandler<MarriageRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, MarriageRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to marriage action without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} marriage request type {RequestType}",
            player.Character.Name, packet.RequestType);

        // TODO: Implement map.MarriageRequest(player, requestType)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (MarriageRequestClientPacket)packet);
    }
}