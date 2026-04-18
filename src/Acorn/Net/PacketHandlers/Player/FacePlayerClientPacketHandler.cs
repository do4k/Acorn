using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
internal class FacePlayerClientPacketHandler : IPacketHandler<FacePlayerClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        FacePlayerClientPacket packet)
    {
        playerState.Character!.Direction = packet.Direction;

        if (playerState.CurrentMap is null)
        {
            return;
        }

        var broadcast = playerState.CurrentMap.Players.Values.Select(player =>
            player.Send(new FacePlayerServerPacket
            {
                Direction = packet.Direction,
                PlayerId = playerState.SessionId
            }));

        await Task.WhenAll(broadcast);
    }

}