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
        if (playerState.CurrentMap is null)
        {
            return;
        }

        // PlayerRange/Request is a players-only Range/Request, so it is answered with a
        // Range/Reply containing just the requested characters (range-filtered).
        await playerState.Send(new RangeReplyServerPacket
        {
            Nearby = playerState.CurrentMap.AsNearbyInfo(playerState, packet.PlayerIds, [])
        });
    }
}
