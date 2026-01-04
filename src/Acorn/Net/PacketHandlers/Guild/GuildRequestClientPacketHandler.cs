using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Guild;

public class GuildRequestClientPacketHandler(
    ILogger<GuildRequestClientPacketHandler> logger,
    IWorldQueries worldQueries)
    : IPacketHandler<GuildRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to guild action without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} guild request: {GuildTag} - {GuildName}",
            player.Character.Name, packet.GuildTag, packet.GuildName);

        // TODO: Implement map.GuildRequest(player, guildTag, guildName)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (GuildRequestClientPacket)packet);
    }
}