using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Npc;

public class NpcRangeRequestClientPacketHandler : IPacketHandler<NpcRangeRequestClientPacket>
{
    private readonly WorldState _world;

    public NpcRangeRequestClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        NpcRangeRequestClientPacket packet)
    {
        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        await playerConnection.Send(new NpcAgreeServerPacket
        {
            Npcs = map.AsNpcMapInfo()
        });
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (NpcRangeRequestClientPacket)packet);
    }
}