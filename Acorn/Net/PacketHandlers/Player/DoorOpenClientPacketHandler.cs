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
    
    public async Task HandleAsync(PlayerConnection playerConnection, DoorOpenClientPacket packet)
    {
        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        await map.BroadcastPacket(new DoorOpenServerPacket()
        {
            Coords = playerConnection.Character?.NextCoords()
        });
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
        => HandleAsync(playerConnection, (DoorOpenClientPacket)packet);
}