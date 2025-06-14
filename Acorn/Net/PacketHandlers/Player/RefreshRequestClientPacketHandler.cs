using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class RefreshRequestClientPacketHandler : IPacketHandler<RefreshRequestClientPacket>
{
    private readonly WorldState _world;

    public RefreshRequestClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        RefreshRequestClientPacket packet)
    {
        await playerConnection.Send(new RefreshReplyServerPacket
        {
            Nearby = _world.MapFor(playerConnection) switch
            {
                { } map => map.AsNearbyInfo(),
                _ => new NearbyInfo()
            }
        });

    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (RefreshRequestClientPacket)packet);
    }
}