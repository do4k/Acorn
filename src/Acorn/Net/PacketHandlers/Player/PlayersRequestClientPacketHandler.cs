using Acorn.Extensions;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using static Moffat.EndlessOnline.SDK.Protocol.Net.Server.InitInitServerPacket;

namespace Acorn.Net.PacketHandlers.Player;

public class PlayersRequestClientPacketHandler : IPacketHandler<PlayersRequestClientPacket>
{
    private readonly IWorldQueries _world;

    public PlayersRequestClientPacketHandler(IWorldQueries world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerState playerState,
        PlayersRequestClientPacket packet)
    {
        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.PlayersList,
            ReplyCodeData = new ReplyCodeDataPlayersList
            {
                PlayersList = new PlayersList
                {
                    Players = _world.GetAllPlayers()
                        .Where(x => x.Character is not null)
                        .Select(x => x.Character!.AsOnlinePlayer())
                        .ToList()
                }
            }
        });
    }

}