using Acorn.Extensions;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class PlayerRangeRequestClientPacketHandler : IPacketHandler<PlayerRangeRequestClientPacket>
{
    public async Task HandleAsync(ConnectionHandler connectionHandler,
        PlayerRangeRequestClientPacket packet)
    {
        if (connectionHandler.CurrentMap is null)
        {
            return;
        }

        await connectionHandler.Send(new PlayersListServerPacket
        {
            PlayersList = new PlayersList
            {
                Players = connectionHandler.CurrentMap.Players.Select(x => x.CharacterController?.AsOnlinePlayer()).ToList()
            }
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (PlayerRangeRequestClientPacket)packet);
    }
}