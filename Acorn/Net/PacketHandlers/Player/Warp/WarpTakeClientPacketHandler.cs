using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Warp;

internal class WarpTakeClientPacketHandler : IPacketHandler<WarpTakeClientPacket>
{
    private readonly ILogger<WarpTakeClientPacketHandler> _logger;
    private readonly WorldState _world;

    public WarpTakeClientPacketHandler(WorldState world, ILogger<WarpTakeClientPacketHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler, WarpTakeClientPacket packet)
    {
        if (packet.SessionId != connectionHandler.SessionId)
        {
            _logger.LogError("Sesison ID was not as expected for player {ConnectionHandler}", connectionHandler.Account?.Username);
            return;
        }

        var foundMap = _world.Maps.TryGetValue(packet.MapId, out var map);
        if (foundMap is false || map is null)
        {
            _logger.LogError("Map with ID {MapId} not found for player {ConnectionHandler}", packet.MapId, connectionHandler.Account?.Username);
            return;
        }

        var writer = new EoWriter();
        map.Data.Serialize(writer);

        await connectionHandler.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.WarpMap,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataWarpMap
            {
                MapFile = new MapFile
                {
                    Content = writer.ToByteArray()
                }
            }
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WarpTakeClientPacket)packet);
    }
}