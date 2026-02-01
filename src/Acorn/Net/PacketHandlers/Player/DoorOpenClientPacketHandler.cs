using Acorn.Extensions;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

public class DoorOpenClientPacketHandler : IPacketHandler<DoorOpenClientPacket>
{
    public DoorOpenClientPacketHandler(IWorldQueries world)
    {
    }

    public async Task HandleAsync(PlayerState playerState, DoorOpenClientPacket packet)
    {
        if (playerState.CurrentMap is null)
        {
            return;
        }

        await playerState.CurrentMap.BroadcastPacket(new DoorOpenServerPacket
        {
            Coords = playerState.Character?.NextCoords()
        });
    }

}