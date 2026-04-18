using Acorn.Data;
using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.StatSkill;

[RequiresCharacter]
public class StatSkillOpenClientPacketHandler(
    ILogger<StatSkillOpenClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    ISkillMasterDataRepository skillMasterDataRepository)
    : IPacketHandler<StatSkillOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, StatSkillOpenClientPacket packet)
    {
        var npcIndex = packet.NpcIndex;
        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc))
        {
            logger.LogWarning("Player {Character} tried to open skill master at invalid NPC index {NpcIndex}",
                player.Character.Name, npcIndex);
            return;
        }

        if (npc.Data.Type != NpcType.Trainer)
        {
            logger.LogWarning("Player {Character} tried to open skill master at non-trainer NPC {NpcId}",
                player.Character.Name, npc.Id);
            return;
        }

        var skillMaster = skillMasterDataRepository.GetByBehaviorId(npc.Data.BehaviorId);
        if (skillMaster == null)
        {
            logger.LogWarning("No skill master data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        logger.LogInformation("Player {Character} opening skill master {SkillMasterName}",
            player.Character.Name, skillMaster.Name);

        player.InteractingNpcIndex = npcIndex;

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
