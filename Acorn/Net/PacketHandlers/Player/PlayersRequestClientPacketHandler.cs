using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using static Moffat.EndlessOnline.SDK.Protocol.Net.Server.InitInitServerPacket;

namespace Acorn.Net.PacketHandlers.Player;

public class PlayersRequestClientPacketHandler : IPacketHandler<PlayersRequestClientPacket>
{
    private readonly WorldState _world;

    public PlayersRequestClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        PlayersRequestClientPacket packet)
    {
        await playerConnection.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.PlayersList,
            ReplyCodeData = new ReplyCodeDataPlayersList
            {
                PlayersList = new PlayersList
                {
                    Players = _world.Players
                        .Where(x => x.Value.Character is not null)
                        .Select(x => x.Value.Character!.AsOnlinePlayer())
                        .ToList()
                }
            }
        });
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (PlayersRequestClientPacket)packet);
    }
}