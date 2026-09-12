using Acorn.Net.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresState(ClientState.Initialized)]
internal class ConnectionPingClientPacketHandler : IPacketHandler<ConnectionPingClientPacket>
{
    public Task HandleAsync(PlayerState playerState, ConnectionPingClientPacket packet)
    {
        playerState.NeedPong = false;
        return Task.CompletedTask;
    }

}