using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

/// <summary>
///     Sent by the client when the player disables whispers. Mirrors eoserv
///     Global_Player (which sets <c>whispers = false</c>).
/// </summary>
internal class GlobalPlayerClientPacketHandler : IPacketHandler<GlobalPlayerClientPacket>
{
    public Task HandleAsync(PlayerState playerState, GlobalPlayerClientPacket packet)
    {
        playerState.Whispers = false;
        return Task.CompletedTask;
    }
}
