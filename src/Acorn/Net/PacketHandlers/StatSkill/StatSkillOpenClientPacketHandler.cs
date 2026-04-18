using Acorn.Data;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.StatSkill;

[RequiresCharacter]
public class StatSkillOpenClientPacketHandler(
    ILogger<StatSkillOpenClientPacketHandler> logger,
    ISkillMasterDataRepository skillMasterDataRepository)
    : IPacketHandler<StatSkillOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, StatSkillOpenClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateAndStartInteraction(player, packet.NpcIndex, NpcType.Trainer, logger);
        if (npc is null) return;

        var skillMaster = skillMasterDataRepository.GetByBehaviorId(npc.Data.BehaviorId);
        if (skillMaster == null)
        {
            logger.LogWarning("No skill master data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        logger.LogInformation("Player {Character} opening skill master {SkillMasterName}",
            player.Character!.Name, skillMaster.Name);

        var skills = skillMaster.Skills.Select(s =>
        {
            // Pad skill requirements to exactly 4
            var reqs = s.SkillRequirements.Take(4).ToList();
            while (reqs.Count < 4)
            {
                reqs.Add(0);
            }

            return new SkillLearn
            {
                Id = s.SkillId,
                LevelRequirement = s.LevelRequirement,
                ClassRequirement = s.ClassRequirement,
                Cost = s.Price,
                SkillRequirements = [reqs[0], reqs[1], reqs[2], reqs[3]],
                StatRequirements = new CharacterBaseStats
                {
                    Str = s.StrRequirement,
                    Intl = s.IntRequirement,
                    Wis = s.WisRequirement,
                    Agi = s.AgiRequirement,
                    Con = s.ConRequirement,
                    Cha = s.ChaRequirement
                }
            };
        }).ToList();

        await player.Send(new StatSkillOpenServerPacket
        {
            SessionId = player.SessionId,
            ShopName = skillMaster.Name,
            Skills = skills
        });
    }
}
