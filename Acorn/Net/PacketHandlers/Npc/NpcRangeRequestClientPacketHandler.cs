using Microsoft.Extensions.Logging;
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

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        NpcRangeRequestClientPacket packet)
    {
        if (connectionHandler.CurrentMap is null)
        {
            _logger.LogWarning("ConnectionHandler {PlayerId} requested NPC range but is not in a map.", connectionHandler.SessionId);
            return;
        }

        await connectionHandler.Send(new NpcAgreeServerPacket
        {
            Npcs = connectionHandler.CurrentMap.AsNpcMapInfo()
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (NpcRangeRequestClientPacket)packet);
    }
}