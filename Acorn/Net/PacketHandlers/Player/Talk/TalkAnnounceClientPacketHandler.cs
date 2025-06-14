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

    public async Task HandleAsync(PlayerConnection playerConnection,
        TalkAnnounceClientPacket packet)
    {
        if (playerConnection.Character is null)
        {
            return;
        }

        if (playerConnection.Character.Admin == AdminLevel.Player)
        {
            _logger.LogDebug("Player tried to send an announcement packet without admin permissions {Player}",
                playerConnection.Character.Name);
            return;
        }

        var announcePackets = _world.Players
            .Where(x => x.Value != playerConnection)
            .Select(async x => await x.Value.Send(new TalkAnnounceServerPacket
            {
                Message = packet.Message,
                PlayerName = playerConnection.Character.Name
            }));

        await Task.WhenAll(announcePackets);

    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (TalkAnnounceClientPacket)packet);
    }
}