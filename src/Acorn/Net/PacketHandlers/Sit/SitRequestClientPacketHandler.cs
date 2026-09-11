using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Sit;

[RequiresCharacter]
public class SitRequestClientPacketHandler(
    ILogger<SitRequestClientPacketHandler> logger,
    IPlayerController playerController)
    : IPacketHandler<SitRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, SitRequestClientPacket packet)
    {
        logger.LogDebug("Player {Character} sit action {SitAction}", player.Character!.Name, packet.SitAction);

        switch (packet.SitAction)
        {
            case SitAction.Sit:
                await playerController.SitAsync(player);
                break;
            case SitAction.Stand:
                await playerController.StandAsync(player);
                break;
        }
    }
}
