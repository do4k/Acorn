using Acorn.Game.Services;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
public class TalkAnnounceClientPacketHandler : IPacketHandler<TalkAnnounceClientPacket>
{
    private readonly IChatSanitizer _chatSanitizer;
    private readonly ILogger<TalkAnnounceClientPacketHandler> _logger;
    private readonly IWorldQueries _world;

    public TalkAnnounceClientPacketHandler(IWorldQueries world, IChatSanitizer chatSanitizer,
        ILogger<TalkAnnounceClientPacketHandler> logger)
    {
        _world = world;
        _chatSanitizer = chatSanitizer;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerState playerState,
        TalkAnnounceClientPacket packet)
    {
        // Announcements require at least Guardian (Spy=1, LightGuide=2, Guardian=3).
        if (playerState.Character!.Admin < AdminLevel.Guardian)
        {
            _logger.LogDebug("Player tried to send an announcement packet without admin permissions {Player}",
                playerState.Character.Name);
            return;
        }

        // Muted players cannot send announcements.
        if (playerState.IsMuted)
        {
            return;
        }

        var message = _chatSanitizer.Sanitize(packet.Message, playerState.Character.Name);

        var announcePackets = _world.GetAllPlayers()
            .Where(x => x != playerState)
            .Select(async x => await x.Send(new TalkAnnounceServerPacket
            {
                Message = message,
                PlayerName = playerState.Character.Name
            }));

        await Task.WhenAll(announcePackets);
    }
}
