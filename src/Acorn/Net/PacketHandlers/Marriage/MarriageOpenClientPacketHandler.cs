using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Marriage;

[RequiresCharacter]
public class MarriageOpenClientPacketHandler(
    IMarriageService marriageService,
    ILogger<MarriageOpenClientPacketHandler> logger)
    : IPacketHandler<MarriageOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, MarriageOpenClientPacket packet)
    {
        logger.LogDebug("Player {Character} opening law office NPC {NpcIndex}",
            player.Character!.Name, packet.NpcIndex);

        await marriageService.OpenLawAsync(player, packet.NpcIndex);
    }
}
