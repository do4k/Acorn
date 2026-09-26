using Acorn.Net.PacketHandlers;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
internal class GlobalCloseClientPacketHandler : IPacketHandler<GlobalCloseClientPacket>
{
    public Task HandleAsync(PlayerState playerState, GlobalCloseClientPacket packet)
    {
        playerState.IsListeningToGlobal = false;
        return Task.CompletedTask;
    }

}