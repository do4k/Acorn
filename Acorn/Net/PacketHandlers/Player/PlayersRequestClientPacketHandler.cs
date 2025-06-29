using Acorn.Extensions;
using Acorn.World;
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

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        PlayersRequestClientPacket packet)
    {
        await connectionHandler.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.PlayersList,
            ReplyCodeData = new ReplyCodeDataPlayersList
            {
                PlayersList = new PlayersList
                {
                    Players = _world.Players
                        .Where(x => x.Value.CharacterController is not null)
                        .Select(x => x.Value.CharacterController!.AsOnlinePlayer())
                        .ToList()
                }
            }
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (PlayersRequestClientPacket)packet);
    }
}