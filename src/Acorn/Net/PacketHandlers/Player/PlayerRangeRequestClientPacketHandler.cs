using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
public class PlayerRangeRequestClientPacketHandler : IPacketHandler<PlayerRangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        PlayerRangeRequestClientPacket packet)
    {
        await playerState.Send(new PlayersListServerPacket
        {
            PlayersList = new PlayersList
            {
                    Players = playerState.CurrentMap!.Players.Values.Select(x => x.Character?.AsOnlinePlayer()).ToList()
            }
        });
    }

}