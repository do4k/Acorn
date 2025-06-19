using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class EmoteReportClientPacketHandler : IPacketHandler<EmoteReportClientPacket>
{
    public Task HandleAsync(PlayerState playerState, EmoteReportClientPacket packet)
    {
        throw new NotImplementedException();
    }

    public Task HandleAsync(PlayerState playerState, object packet)
        => HandleAsync(playerState, (EmoteReportClientPacket)packet);
}