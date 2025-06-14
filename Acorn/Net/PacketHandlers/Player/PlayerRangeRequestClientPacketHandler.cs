using Acorn.Extensions;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PlayerRangeRequestClientPacketHandler : IPacketHandler<PlayerRangeRequestClientPacket>
{
    private readonly WorldState _world;

    public PlayerRangeRequestClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        PlayerRangeRequestClientPacket packet)
    {
        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        await playerConnection.Send(new PlayersListServerPacket
        {
            PlayersList = new PlayersList
            {
                Players = map.Players.Select(x => x.Character?.AsOnlinePlayer()).ToList()
            }
        });
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (PlayerRangeRequestClientPacket)packet);
    }
}