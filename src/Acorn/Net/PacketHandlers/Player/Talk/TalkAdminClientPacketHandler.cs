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
internal class TalkAdminClientPacketHandler(
    IWorldQueries world,
    IChatSanitizer chatSanitizer,
    ILogger<TalkAdminClientPacketHandler> logger) : IPacketHandler<TalkAdminClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkAdminClientPacket packet)
    {
        // Admin chat requires at least Guardian (Spy=1, LightGuide=2, Guardian=3).
        if (playerState.Character!.Admin < AdminLevel.Guardian)
        {
            logger.LogDebug("Player tried to send an admin message without admin permissions {Player}",
                playerState.Character.Name);
            return;
        }

        // Muted players cannot use admin chat.
        if (playerState.IsMuted)
        {
            return;
        }

        var message = chatSanitizer.Sanitize(packet.Message, playerState.Character.Name);

        var broadcast = world.GetAllPlayers()
            .Where(x => x != playerState && x.Character?.Admin >= AdminLevel.Guardian)
            .Select(x => x.Send(new TalkAdminServerPacket
            {
                Message = message,
                PlayerName = playerState.Character.Name!
            }));

        await Task.WhenAll(broadcast);
    }
}
