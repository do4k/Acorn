using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class ConnectionPingClientPacketHandler : IPacketHandler<ConnectionPingClientPacket>
{
    public Task HandleAsync(PlayerState playerState, ConnectionPingClientPacket packet)
    {
        playerState.NeedPong = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (ConnectionPingClientPacket)packet);
    }
}