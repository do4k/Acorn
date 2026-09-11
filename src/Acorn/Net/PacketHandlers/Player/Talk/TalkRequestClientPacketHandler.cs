using Acorn.Game.Services;
using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Handles guild chat messages. Broadcasts to all online players in the same guild.
/// </summary>
[RequiresCharacter]
internal class TalkRequestClientPacketHandler(
    IGuildService guildService,
    IChatSanitizer chatSanitizer) : IPacketHandler<TalkRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkRequestClientPacket packet)
    {
        if (playerState.Character!.GuildTag is null) return;

        // Muted players cannot use guild chat.
        if (playerState.IsMuted)
        {
            return;
        }

        var message = chatSanitizer.Sanitize(packet.Message, playerState.Character.Name);
        await guildService.SendGuildMessage(playerState, message);
    }
}
