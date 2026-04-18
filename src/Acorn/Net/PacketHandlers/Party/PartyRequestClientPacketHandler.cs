using Acorn.World.Services.Party;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Party;

[RequiresCharacter]
public class PartyRequestClientPacketHandler(
    IPartyService partyService,
    ILogger<PartyRequestClientPacketHandler> logger)
    : IPacketHandler<PartyRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, PartyRequestClientPacket packet)
    {
        logger.LogDebug("Player {Character} sending party {Type} request to {Target}",
            player.Character!.Name, packet.RequestType, packet.PlayerId);

        await partyService.RequestParty(player, packet.PlayerId, packet.RequestType);
    }
}
