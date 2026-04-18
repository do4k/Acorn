using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Priest;

[RequiresCharacter]
public class PriestRequestClientPacketHandler(
    IMarriageService marriageService,
    ILogger<PriestRequestClientPacketHandler> logger)
    : IPacketHandler<PriestRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, PriestRequestClientPacket packet)
    {
        if (player.SessionId != packet.SessionId)
        {
            return;
        }

        logger.LogDebug("Player {Character} requesting wedding with {Partner}",
            player.Character!.Name, packet.Name);

        await marriageService.RequestWeddingAsync(player, packet.Name);
    }
}
