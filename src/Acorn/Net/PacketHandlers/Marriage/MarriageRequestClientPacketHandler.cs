using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Marriage;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Marriage;

[RequiresCharacter]
public class MarriageRequestClientPacketHandler(
    IMarriageService marriageService,
    ILogger<MarriageRequestClientPacketHandler> logger)
    : IPacketHandler<MarriageRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, MarriageRequestClientPacket packet)
    {
        if (player.SessionId != packet.SessionId)
        {
            return;
        }

        var name = packet.Name.ToLowerInvariant();

        logger.LogDebug("Player {Character} marriage request type {RequestType} for {Name}",
            player.Character!.Name, packet.RequestType, name);

        switch (packet.RequestType)
        {
            case MarriageRequestType.MarriageApproval:
                await marriageService.RequestMarriageApprovalAsync(player, name);
                break;
            case MarriageRequestType.Divorce:
                await marriageService.RequestDivorceAsync(player, name);
                break;
        }
    }
}
