using Acorn.Data;
using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Citizen;

public class CitizenOpenClientPacketHandler(
    ILogger<CitizenOpenClientPacketHandler> logger,
    IInnDataRepository innDataRepository)
    : IPacketHandler<CitizenOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open inn without character or map",
                player.SessionId);
            return;
        }

        var npcIndex = packet.NpcIndex;

        // Find the NPC by index on the map
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null)
        {
            logger.LogWarning("Player {Character} tried to open inn at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        // Verify it's an Inn NPC
        if (npc.npc.Data.Type != NpcType.Inn)
        {
            logger.LogWarning("Player {Character} tried to open inn at non-inn NPC {NpcId}",
                player.Character.Name, npc.npc.Id);
            return;
        }

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.npc.Data.BehaviorId);
            return;
        }

        // Get current home inn
        var currentHome = player.Character.Home ?? innDataRepository.DefaultHomeName;
        var currentInn = innDataRepository.GetInnByName(currentHome);
        var currentHomeId = currentInn?.BehaviorId ?? 0;

        // Store the NPC index for subsequent operations
        player.InteractingNpcIndex = npcIndex;

        logger.LogInformation("Player {Character} opening inn {InnName}",
            player.Character.Name, inn.Name);

        // Build questions list (pad to 3 if needed)
        var questions = inn.Questions.Take(3).Select(q => q.Question).ToList();
        while (questions.Count < 3)
        {
            questions.Add("");
        }

        await player.Send(new CitizenOpenServerPacket
        {
            BehaviorId = inn.BehaviorId + 1, // Client expects 1-based
            CurrentHomeId = currentHomeId > 0 ? currentHomeId - 1 : 0, // Client expects 0-based
            SessionId = player.SessionId,
            Questions = questions
        });
    }

}
