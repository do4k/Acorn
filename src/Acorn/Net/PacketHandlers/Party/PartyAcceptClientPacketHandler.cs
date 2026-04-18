using Acorn.World.Services.Party;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Party;

[RequiresCharacter]
public class PartyAcceptClientPacketHandler(
    IPartyService partyService,
    ILogger<PartyAcceptClientPacketHandler> logger)
    : IPacketHandler<PartyAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState player, PartyAcceptClientPacket packet)
    {
        logger.LogDebug("Player {Character} accepting party {Type} request from {Inviter}",
            player.Character!.Name, packet.RequestType, packet.InviterPlayerId);

        await partyService.AcceptPartyRequest(player, packet.InviterPlayerId, packet.RequestType);
    }
}
