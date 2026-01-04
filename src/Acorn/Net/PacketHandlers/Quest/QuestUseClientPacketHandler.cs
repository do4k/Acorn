using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Quest;

public class QuestUseClientPacketHandler(ILogger<QuestUseClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<QuestUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, QuestUseClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to interact with quest without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} using quest {QuestId}",
            player.Character.Name, packet.QuestId);

        // TODO: Implement map.UseQuest(player, questId)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (QuestUseClientPacket)packet);
    }
}