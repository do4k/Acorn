using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class GlobalCloseClientPacketHandler : IPacketHandler<GlobalCloseClientPacket>
{
    public Task HandleAsync(PlayerState playerState, GlobalCloseClientPacket packet)
    {
        playerState.IsListeningToGlobal = false;
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (GlobalCloseClientPacket)packet);
    }
}