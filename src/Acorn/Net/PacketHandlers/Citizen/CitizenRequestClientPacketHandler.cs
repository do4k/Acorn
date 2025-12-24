using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Citizen;

public class CitizenRequestClientPacketHandler(ILogger<CitizenRequestClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<CitizenRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted citizen request without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} requesting citizenship {BehaviorId}",
            player.Character.Name, packet.BehaviorId);

        // TODO: Handle citizenship requests
        // TODO: Update player home town
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (CitizenRequestClientPacket)packet);
    }
}

