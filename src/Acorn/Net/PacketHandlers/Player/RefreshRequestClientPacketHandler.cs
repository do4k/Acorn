using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class RefreshRequestClientPacketHandler : IPacketHandler<RefreshRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        RefreshRequestClientPacket packet)
    {
        await playerState.Send(new RefreshReplyServerPacket
        {
            Nearby = playerState.CurrentMap switch
            {
                { } map => map.AsNearbyInfo(),
                _ => new NearbyInfo()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (RefreshRequestClientPacket)packet);
    }
}