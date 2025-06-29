using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class TalkAnnounceClientPacketHandler : IPacketHandler<TalkAnnounceClientPacket>
{
    private readonly ILogger<TalkAnnounceClientPacketHandler> _logger;
    private readonly WorldState _world;

    public TalkAnnounceClientPacketHandler(WorldState world, ILogger<TalkAnnounceClientPacketHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler,
        TalkAnnounceClientPacket packet)
    {
        if (connectionHandler.CharacterController is null)
        {
            _logger.LogError("ConnectionHandler state has no Character initialised.");
            return;
        }

        if (connectionHandler.CharacterController.Data.Admin == AdminLevel.Player)
        {
            _logger.LogDebug("ConnectionHandler tried to send an announcement packet without admin permissions {ConnectionHandler}",
                connectionHandler.CharacterController.Data.Name);
            return;
        }

        var announcePackets = _world.Players
            .Where(x => x.Value != connectionHandler)
            .Select(async x => await x.Value.Send(new TalkAnnounceServerPacket
            {
                Message = packet.Message,
                PlayerName = connectionHandler.CharacterController.Data.Name
            }));

        await Task.WhenAll(announcePackets);

    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (TalkAnnounceClientPacket)packet);
    }
}