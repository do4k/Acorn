using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Npc;

[RequiresCharacter]
public class NpcRangeRequestClientPacketHandler : IPacketHandler<NpcRangeRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        NpcRangeRequestClientPacket packet)
    {
        if (playerState.CurrentMap is null)
        {
            return;
        }

        // Only return the NPCs the client actually asked for (and that are still in view).
        await playerState.Send(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap.AsNearbyInfo(playerState, [], packet.NpcIndexes).Npcs
        });
    }
}
