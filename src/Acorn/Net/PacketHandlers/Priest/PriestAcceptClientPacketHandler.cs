using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Priest;

[RequiresCharacter]
public class PriestAcceptClientPacketHandler(
    IMarriageService marriageService,
    ILogger<PriestAcceptClientPacketHandler> logger)
    : IPacketHandler<PriestAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState player, PriestAcceptClientPacket packet)
    {
        if (player.SessionId != packet.SessionId)
        {
            return;
        }

        logger.LogDebug("Player {Character} accepting wedding request",
            player.Character!.Name);

        await marriageService.AcceptWeddingRequestAsync(player);
    }
}
