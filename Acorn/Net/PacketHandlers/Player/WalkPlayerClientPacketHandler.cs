using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WalkPlayerClientPacketHandler : IPacketHandler<WalkPlayerClientPacket>
{
    private readonly ILogger<WalkPlayerClientPacketHandler> _logger;
    private readonly WorldState _world;

    public WalkPlayerClientPacketHandler(ILogger<WalkPlayerClientPacketHandler> logger, WorldState world)
    {
        _logger = logger;
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        WalkPlayerClientPacket packet)
    {
        if (playerConnection.Character is null)
        {
            _logger.LogError(
                "Tried to handler player walk, but the character associated with this connection has not been initialised");
            return;
        }

        playerConnection.Character.X = packet.WalkAction.Direction switch
        {
            Direction.Left => playerConnection.Character.X - 1,
            Direction.Right => playerConnection.Character.X + 1,
            _ => playerConnection.Character.X
        };
        playerConnection.Character.Y = packet.WalkAction.Direction switch
        {
            Direction.Up => playerConnection.Character.Y - 1,
            Direction.Down => playerConnection.Character.Y + 1,
            _ => playerConnection.Character.Y
        };

        playerConnection.Character.Direction = packet.WalkAction.Direction;

        var map = _world.MapFor(playerConnection);
        var otherPlayers = map.Players
            .Where(x => x.Character?.Map == map.Id).Where(x => x != playerConnection).ToList();

        var otherPlayerTasks = otherPlayers.Select(async otherPlayer =>
        {
            await otherPlayer.Send(new WalkPlayerServerPacket
            {
                Direction = playerConnection.Character.Direction,
                PlayerId = playerConnection.SessionId,
                Coords = new Coords
                {
                    X = playerConnection.Character.X,
                    Y = playerConnection.Character.Y
                }
            });
        });

        await Task.WhenAll(otherPlayerTasks);
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (WalkPlayerClientPacket)packet);
    }
}