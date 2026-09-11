using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

[RequiresCharacter]
public class DoorOpenClientPacketHandler(IDoorService doorService) : IPacketHandler<DoorOpenClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, DoorOpenClientPacket packet)
    {
        if (playerState.CurrentMap is null || playerState.Character is null)
        {
            return;
        }

        // Use the coordinates the client actually sent rather than the player's facing tile.
        await doorService.OpenDoorAsync(playerState, packet.Coords, playerState.CurrentMap);
    }
}
