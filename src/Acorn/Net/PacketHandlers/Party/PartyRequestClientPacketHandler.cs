using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Party;

public class PartyRequestClientPacketHandler(
    ILogger<PartyRequestClientPacketHandler> logger,
    IWorldQueries worldQueries)
    : IPacketHandler<PartyRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, PartyRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to party action without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} initiating party with type {RequestType}",
            player.Character.Name, packet.RequestType);

        // TODO: Implement map.PartyRequest(player, requestType, playerId)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (PartyRequestClientPacket)packet);
    }
}