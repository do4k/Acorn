using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

internal class GlobalCloseClientPacketHandler : IPacketHandler<GlobalCloseClientPacket>
{
    public Task HandleAsync(PlayerState playerState, GlobalCloseClientPacket packet)
    {
        playerState.IsListeningToGlobal = false;
        return Task.CompletedTask;
    }

}