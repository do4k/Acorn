using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Locker;

public class LockerOpenClientPacketHandler(ILogger<LockerOpenClientPacketHandler> logger)
    : IPacketHandler<LockerOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, LockerOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open locker without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening locker at ({X}, {Y})",
            player.Character.Name, packet.LockerCoords.X, packet.LockerCoords.Y);

        // TODO: Implement map.OpenLocker(player, lockerCoords)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (LockerOpenClientPacket)packet);
    }
}