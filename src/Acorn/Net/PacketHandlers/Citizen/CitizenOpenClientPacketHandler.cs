using Acorn.Data;
using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Citizen;

[RequiresCharacter]
public class CitizenOpenClientPacketHandler(
    ILogger<CitizenOpenClientPacketHandler> logger,
    IInnDataRepository innDataRepository)
    : IPacketHandler<CitizenOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenOpenClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateAndStartInteraction(player, packet.NpcIndex, NpcType.Inn, logger);
        if (npc is null) return;

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        // Get current home inn
        var currentHome = player.Character.Home ?? innDataRepository.DefaultHomeName;
        var currentInn = innDataRepository.GetInnByName(currentHome);
        var currentHomeId = currentInn?.BehaviorId ?? 0;

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
