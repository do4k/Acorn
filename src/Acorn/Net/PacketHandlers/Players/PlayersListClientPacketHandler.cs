using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using static Moffat.EndlessOnline.SDK.Protocol.Net.Server.InitInitServerPacket;

namespace Acorn.Net.PacketHandlers.Players;

/// <summary>
///     Handles Players_List - returns a list of online character names.
///     This is the "friends list" variant (names only), as opposed to
///     PlayersRequest which returns the full detailed player list.
/// </summary>
[RequiresCharacter]
internal class PlayersListClientPacketHandler(IWorldQueries world)
    : IPacketHandler<PlayersListClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, PlayersListClientPacket packet)
    {
        var playerNames = world.GetAllPlayers()
            .Where(x => x.Character?.Name is not null)
            .Select(x => x.Character!.Name!)
            .ToList();

        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.PlayersListFriends,
            ReplyCodeData = new ReplyCodeDataPlayersListFriends
            {
                PlayersList = new PlayersListFriends
                {
                    Players = playerNames
                }
            }
        });
    }
}
