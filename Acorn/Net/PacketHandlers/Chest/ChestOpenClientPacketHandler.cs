using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Chest;

public class ChestOpenClientPacketHandler(ILogger<ChestOpenClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<ChestOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ChestOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open chest without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening chest at ({X}, {Y})",
            player.Character.Name, packet.Coords.X, packet.Coords.Y);

        // TODO: Implement map.OpenChest(player, coords)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ChestOpenClientPacket)packet);
    }
}
