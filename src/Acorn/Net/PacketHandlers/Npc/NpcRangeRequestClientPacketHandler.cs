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
        await playerState.Send(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap!.AsNpcMapInfo()
        });
    }

}
