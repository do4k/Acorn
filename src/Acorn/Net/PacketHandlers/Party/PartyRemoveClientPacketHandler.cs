using Acorn.World.Services.Party;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Party;

[RequiresCharacter]
public class PartyRemoveClientPacketHandler(
    IPartyService partyService,
    ILogger<PartyRemoveClientPacketHandler> logger)
    : IPacketHandler<PartyRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, PartyRemoveClientPacket packet)
    {
        logger.LogDebug("Player {Character} removing {Target} from party",
            player.Character.Name, packet.PlayerId);

        await partyService.RemoveFromParty(player, packet.PlayerId);
    }
}
