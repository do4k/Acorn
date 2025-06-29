using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class EmoteReportClientPacketHandler : IPacketHandler<EmoteReportClientPacket>
{
    public Task HandleAsync(ConnectionHandler connectionHandler, EmoteReportClientPacket packet)
    {
        throw new NotImplementedException();
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
        => HandleAsync(connectionHandler, (EmoteReportClientPacket)packet);
}