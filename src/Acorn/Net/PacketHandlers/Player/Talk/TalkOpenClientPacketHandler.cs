using Acorn.World;
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
    IPartyService partyService) : IPacketHandler<TalkOpenClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkOpenClientPacket packet)
    {
        await partyService.SendPartyMessage(playerState, packet.Message);
    }
}
