using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Priest;

[RequiresCharacter]
public class PriestUseClientPacketHandler(
    IMarriageService marriageService,
    ILogger<PriestUseClientPacketHandler> logger)
    : IPacketHandler<PriestUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, PriestUseClientPacket packet)
    {
        if (player.SessionId != packet.SessionId)
        {
            return;
        }

        logger.LogDebug("Player {Character} says 'I do'",
            player.Character!.Name);

        await marriageService.SayIDoAsync(player);
    }
}
