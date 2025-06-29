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

    public async Task HandleAsync(ConnectionHandler connectionHandler, DoorOpenClientPacket packet)
    {
        if (connectionHandler.CurrentMap is null)
        {
            return;
        }

        await connectionHandler.CurrentMap.BroadcastPacket(new DoorOpenServerPacket
        {
            Coords = connectionHandler.CharacterController?.NextCoords()
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
        => HandleAsync(connectionHandler, (DoorOpenClientPacket)packet);
}