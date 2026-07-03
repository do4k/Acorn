using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
public class EmoteReportClientPacketHandler : IPacketHandler<EmoteReportClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, EmoteReportClientPacket packet)
    {
        if (playerState.CurrentMap is null)
        {
            return;
        }

        var broadcast = playerState.CurrentMap.Players.Values
            .Where(player => player.SessionId != playerState.SessionId)
            .Select(player => player.Send(new EmotePlayerServerPacket
            {
                PlayerId = playerState.SessionId,
                Emote = packet.Emote
            }));

        await Task.WhenAll(broadcast);
    }

}