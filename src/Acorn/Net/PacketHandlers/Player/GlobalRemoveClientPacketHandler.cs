using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

/// <summary>
///     Sent by the client when the player re-enables whispers. Mirrors eoserv
///     Global_Remove (which sets <c>whispers = true</c>).
/// </summary>
internal class GlobalRemoveClientPacketHandler : IPacketHandler<GlobalRemoveClientPacket>
{
    public Task HandleAsync(PlayerState playerState, GlobalRemoveClientPacket packet)
    {
        playerState.Whispers = true;
        return Task.CompletedTask;
    }
}
