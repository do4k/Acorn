using Acorn.Game.Services;
using Acorn.World.Services.Party;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Handles party chat messages. Broadcasts to all party members.
/// </summary>
[RequiresCharacter]
internal class TalkOpenClientPacketHandler(
    IPartyService partyService,
    IChatSanitizer chatSanitizer) : IPacketHandler<TalkOpenClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkOpenClientPacket packet)
    {
        // Muted players cannot use party chat.
        if (playerState.IsMuted)
        {
            return;
        }

        var message = chatSanitizer.Sanitize(packet.Message, playerState.Character?.Name);
        await partyService.SendPartyMessage(playerState, message);
    }
}
