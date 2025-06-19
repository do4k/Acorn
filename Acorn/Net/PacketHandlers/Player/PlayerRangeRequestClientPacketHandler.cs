using Acorn.Extensions;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PlayerRangeRequestClientPacketHandler : IPacketHandler<PlayerRangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        PlayerRangeRequestClientPacket packet)
    {
        if (playerState.CurrentMap is null)
        {
            return;
        }

        await playerState.Send(new PlayersListServerPacket
        {
            PlayersList = new PlayersList
            {
                Players = playerState.CurrentMap.Players.Select(x => x.Character?.AsOnlinePlayer()).ToList()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (PlayerRangeRequestClientPacket)packet);
    }
}