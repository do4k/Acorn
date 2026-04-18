using Acorn.Net.PacketHandlers;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Quest;

[RequiresCharacter]
public class QuestUseClientPacketHandler(
    IQuestService questService,
    ILogger<QuestUseClientPacketHandler> logger)
    : IPacketHandler<QuestUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, QuestUseClientPacket packet)
    {
        logger.LogDebug("Player {Character} talking to quest NPC index {NpcIndex} (quest {QuestId})",
            player.Character!.Name, packet.NpcIndex, packet.QuestId);

        await questService.TalkToQuestNpc(player, packet.NpcIndex, packet.QuestId);
    }
}
