using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Quest;

[RequiresCharacter]
public class QuestListClientPacketHandler(
    IQuestService questService,
    ILogger<QuestListClientPacketHandler> logger)
    : IPacketHandler<QuestListClientPacket>
{
    public async Task HandleAsync(PlayerState player, QuestListClientPacket packet)
    {
        logger.LogDebug("Player {Character} requesting quest list (page {Page})",
            player.Character!.Name, packet.Page);

        switch (packet.Page)
        {
            case QuestPage.Progress:
                await questService.ViewQuestProgress(player);
                break;
            case QuestPage.History:
                await questService.ViewQuestHistory(player);
                break;
            default:
                logger.LogWarning("Unknown quest page: {Page}", packet.Page);
                break;
        }
    }
}
