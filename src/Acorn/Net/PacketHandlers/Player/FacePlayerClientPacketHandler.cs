using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class FacePlayerClientPacketHandler : IPacketHandler<FacePlayerClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        FacePlayerClientPacket packet)
    {
        if (playerState.Character is null)
        {
            return;
        }

        playerState.Character.Direction = packet.Direction;

        if (playerState.CurrentMap is null)
        {
            return;
        }

        var broadcast = playerState.CurrentMap.Players.Select(player =>
            player.Send(new FacePlayerServerPacket
            {
                Direction = packet.Direction,
                PlayerId = playerState.SessionId
            }));

        await Task.WhenAll(broadcast);
    }

}