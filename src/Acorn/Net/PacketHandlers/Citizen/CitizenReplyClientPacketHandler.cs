using Acorn.Data;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Citizen;

[RequiresCharacter]
public class CitizenReplyClientPacketHandler(
    ILogger<CitizenReplyClientPacketHandler> logger,
    IInnDataRepository innDataRepository)
    : IPacketHandler<CitizenReplyClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenReplyClientPacket packet)
    {
        var npcIndex = player.InteractingNpcIndex;
        if (npcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted citizenship reply without interacting NPC",
                player.Character.Name);
            return;
        }

        // Find the NPC by index on the map
        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex.Value, out var npc))
        {
            logger.LogWarning("Player {Character} tried to reply citizenship at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's an Inn NPC
        if (npc.Data.Type != NpcType.Inn)
        {
            logger.LogWarning("Player {Character} tried to reply citizenship at non-inn NPC {NpcId}",
                player.Character.Name, npc.Id);
            return;
        }

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        // Check answers against inn questions
        var answers = packet.Answers ?? [];
        var questionsWrong = 0;

        for (var i = 0; i < Math.Min(3, inn.Questions.Count); i++)
        {
            var expectedAnswer = inn.Questions[i].Answer;
            var providedAnswer = i < answers.Count ? answers[i] : "";

            if (!string.Equals(expectedAnswer, providedAnswer, StringComparison.OrdinalIgnoreCase))
            {
                questionsWrong++;
            }
        }

        if (questionsWrong == 0)
        {
            // All answers correct - set new home
            player.Character.Home = inn.Name;
            logger.LogInformation("Player {Character} became citizen of {InnName}",
                player.Character.Name, inn.Name);
        }
        else
        {
            logger.LogInformation("Player {Character} failed citizenship questions for {InnName} ({QuestionsWrong} wrong)",
                player.Character.Name, inn.Name, questionsWrong);
        }

        await player.Send(new CitizenReplyServerPacket
        {
            QuestionsWrong = questionsWrong
        });
    }

}
