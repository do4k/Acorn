using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Chest;

public class ChestOpenClientPacketHandler(ILogger<ChestOpenClientPacketHandler> logger)
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

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ChestOpenClientPacket)packet);
    }
}