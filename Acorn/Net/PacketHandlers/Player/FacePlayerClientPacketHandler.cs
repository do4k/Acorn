using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class FacePlayerClientPacketHandler : IPacketHandler<FacePlayerClientPacket>
{
    private readonly WorldState _world;

    public FacePlayerClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        FacePlayerClientPacket packet)
    {
        if (playerConnection.Character is null)
        {
            return;
        }

        playerConnection.Character.Direction = packet.Direction;

        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        var broadcast = map.Players.ToList().Select(player =>
            player.Send(new FacePlayerServerPacket
            {
                Direction = packet.Direction,
                PlayerId = playerConnection.SessionId
            }));

        await Task.WhenAll(broadcast);

    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (FacePlayerClientPacket)packet);
    }
}