using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Npc;

public class NpcRangeRequestClientPacketHandler : IPacketHandler<NpcRangeRequestClientPacket>
{
    private readonly ILogger<NpcRangeRequestClientPacketHandler> _logger;

    public NpcRangeRequestClientPacketHandler(ILogger<NpcRangeRequestClientPacketHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        NpcRangeRequestClientPacket packet)
    {
        if (playerState.CurrentMap is null)
        {
            _logger.LogWarning("Player {PlayerId} requested Npc range but is not in a map.", playerState.SessionId);
            return;
        }

        await playerState.Send(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap.AsNpcMapInfo()
        });
    }

}