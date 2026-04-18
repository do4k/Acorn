using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Barber;

[RequiresCharacter]
public class BarberOpenClientPacketHandler(
    ILogger<BarberOpenClientPacketHandler> logger)
    : IPacketHandler<BarberOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BarberOpenClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateAndStartInteraction(player, packet.NpcIndex, NpcType.Barber, logger);
        if (npc is null) return;

        logger.LogInformation("Player {Character} opening barber", player.Character.Name);

        await player.Send(new BarberOpenServerPacket
        {
            SessionId = player.SessionId
        });
    }

}
