using Acorn.World.Services.Party;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Party;

[RequiresCharacter]
public class PartyTakeClientPacketHandler(
    IPartyService partyService,
    ILogger<PartyTakeClientPacketHandler> logger)
    : IPacketHandler<PartyTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, PartyTakeClientPacket packet)
    {
        logger.LogDebug("Player {Character} requesting party member list refresh",
            player.Character!.Name);

        await partyService.RefreshPartyList(player);
    }
}
