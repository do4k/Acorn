using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class TalkAnnounceClientPacketHandler : IPacketHandler<TalkAnnounceClientPacket>
{
    private readonly ILogger<TalkAnnounceClientPacketHandler> _logger;
    private readonly IWorldQueries _world;

    public TalkAnnounceClientPacketHandler(IWorldQueries world, ILogger<TalkAnnounceClientPacketHandler> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        TalkAnnounceClientPacket packet)
    {
        if (playerState.Character is null)
        {
            return;
        }

        if (playerState.Character.Admin == AdminLevel.Player)
        {
            _logger.LogDebug("Player tried to send an announcement packet without admin permissions {Player}",
                playerState.Character.Name);
            return;
        }

        var announcePackets = _world.GetAllPlayers()
            .Where(x => x != playerState)
            .Select(async x => await x.Send(new TalkAnnounceServerPacket
            {
                Message = packet.Message,
                PlayerName = playerState.Character.Name
            }));

        await Task.WhenAll(announcePackets);

    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (TalkAnnounceClientPacket)packet);
    }
}