using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.AdminInteract;

public class AdminInteractTellClientPacketHandler(ILogger<AdminInteractTellClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<AdminInteractTellClientPacket>
{
    public async Task HandleAsync(PlayerState player, AdminInteractTellClientPacket packet)
    {
        if (player.Character == null)
        {
            logger.LogWarning("Player {SessionId} attempted admin interact without character", player.SessionId);
            return;
        }

        logger.LogInformation("Admin {Character} using admin interact command",
            player.Character.Name);

        // TODO: Validate player has admin privileges
        // TODO: Execute admin command
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (AdminInteractTellClientPacket)packet);
    }
}