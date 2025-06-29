using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class RefreshRequestClientPacketHandler : IPacketHandler<RefreshRequestClientPacket>
{
    public async Task HandleAsync(ConnectionHandler connectionHandler,
        RefreshRequestClientPacket packet)
    {
        await connectionHandler.Send(new RefreshReplyServerPacket
        {
            Nearby = connectionHandler.CurrentMap switch
            {
                { } map => map.AsNearbyInfo(),
                _ => new NearbyInfo()
            }
        });

    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (RefreshRequestClientPacket)packet);
    }
}