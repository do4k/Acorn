using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class GlobalCloseClientPacketHandler : IPacketHandler<GlobalCloseClientPacket>
{
    public Task HandleAsync(PlayerConnection playerConnection, GlobalCloseClientPacket packet)
    {
        playerConnection.IsListeningToGlobal = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (GlobalCloseClientPacket)packet);
    }
}