using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Priest;

public class PriestRequestClientPacketHandler(ILogger<PriestRequestClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<PriestRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, PriestRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to priest action without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} priest request for character {Name}",
            player.Character.Name, packet.Name);

        // TODO: Implement map.PriestRequest(player, characterName)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (PriestRequestClientPacket)packet);
    }
}
