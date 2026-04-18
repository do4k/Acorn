using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Priest;

[RequiresCharacter]
public class PriestOpenClientPacketHandler(
    IMarriageService marriageService,
    ILogger<PriestOpenClientPacketHandler> logger)
    : IPacketHandler<PriestOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, PriestOpenClientPacket packet)
    {
        logger.LogDebug("Player {Character} opening priest NPC {NpcIndex}",
            player.Character!.Name, packet.NpcIndex);

        await marriageService.OpenPriestAsync(player, packet.NpcIndex);
    }
}
