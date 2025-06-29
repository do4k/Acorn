using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class GlobalCloseClientPacketHandler : IPacketHandler<GlobalCloseClientPacket>
{
    public Task HandleAsync(ConnectionHandler connectionHandler, GlobalCloseClientPacket packet)
    {
        connectionHandler.IsListeningToGlobal = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (GlobalCloseClientPacket)packet);
    }
}