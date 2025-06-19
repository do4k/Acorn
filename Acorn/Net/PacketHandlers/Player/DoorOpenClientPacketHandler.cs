using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class DoorOpenClientPacketHandler : IPacketHandler<DoorOpenClientPacket>
{
    private readonly WorldState _world;

    public DoorOpenClientPacketHandler(WorldState world)
    {
        _world = world;
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

    public Task HandleAsync(PlayerState playerState, object packet)
        => HandleAsync(playerState, (DoorOpenClientPacket)packet);
}