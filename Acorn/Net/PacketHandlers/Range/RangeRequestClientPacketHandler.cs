using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Range;

public class RangeRequestClientPacketHandler(ILogger<RangeRequestClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<RangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, RangeRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted range request without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} requesting range for player IDs",
            player.Character.Name);

        // TODO: Send player/NPC data for requested entities
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (RangeRequestClientPacket)packet);
    }
}
