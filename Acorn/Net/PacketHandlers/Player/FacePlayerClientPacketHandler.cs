using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class FacePlayerClientPacketHandler : IPacketHandler<FacePlayerClientPacket>
{
    public async Task HandleAsync(ConnectionHandler connectionHandler,
        FacePlayerClientPacket packet)
    {
        if (connectionHandler.CharacterController is null)
        {
            return;
        }

        connectionHandler.CharacterController.SetDirection(packet.Direction);

        if (connectionHandler.CurrentMap is null)
        {
            return;
        }

        var broadcast = connectionHandler.CurrentMap.Players.Select(player =>
            player.Send(new FacePlayerServerPacket
            {
                Direction = packet.Direction,
                PlayerId = connectionHandler.SessionId
            }));

        await Task.WhenAll(broadcast);

    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (FacePlayerClientPacket)packet);
    }
}