using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class ConnectionPingClientPacketHandler : IPacketHandler<ConnectionPingClientPacket>
{
    public Task HandleAsync(ConnectionHandler connectionHandler, ConnectionPingClientPacket packet)
    {
        connectionHandler.NeedPong = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (ConnectionPingClientPacket)packet);
    }
}