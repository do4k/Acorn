using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class ConnectionPingClientPacketHandler : IPacketHandler<ConnectionPingClientPacket>
{
    public Task HandleAsync(PlayerConnection playerConnection, ConnectionPingClientPacket packet)
    {
        playerConnection.NeedPong = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (ConnectionPingClientPacket)packet);
    }
}