using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Barber;

public class BarberOpenClientPacketHandler(ILogger<BarberOpenClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<BarberOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BarberOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open barber without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening barber at NPC index {NpcIndex}",
            player.Character.Name, packet.NpcIndex);

        // TODO: Validate NPC is a barber
        // TODO: Send available styles to player
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (BarberOpenClientPacket)packet);
    }
}