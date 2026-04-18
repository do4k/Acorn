using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Quest;

[RequiresCharacter]
public class QuestAcceptClientPacketHandler(
    IQuestService questService,
    ILogger<QuestAcceptClientPacketHandler> logger)
    : IPacketHandler<QuestAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState player, QuestAcceptClientPacket packet)
    {
        logger.LogDebug("Player {Character} replying to quest {QuestId} (session {SessionId})",
            player.Character!.Name, packet.QuestId, packet.SessionId);

        // Extract action ID from reply type data if it's a link response
        int? actionId = packet.ReplyTypeData is QuestAcceptClientPacket.ReplyTypeDataLink linkData
            ? linkData.Action
            : null;

        await questService.ReplyToQuestNpc(player, packet.SessionId, packet.QuestId, actionId);
    }
}
