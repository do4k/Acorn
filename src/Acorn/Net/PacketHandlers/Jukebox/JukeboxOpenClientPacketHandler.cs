using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Jukebox;

public class JukeboxOpenClientPacketHandler(ILogger<JukeboxOpenClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<JukeboxOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, JukeboxOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open jukebox without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening jukebox at coords ({X}, {Y})",
            player.Character.Name, packet.Coords.X, packet.Coords.Y);

        // TODO: Validate jukebox exists
        // TODO: Send available music tracks
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (JukeboxOpenClientPacket)packet);
    }
}