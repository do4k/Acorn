using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.StatSkill;

[RequiresCharacter]
public class StatSkillTakeClientPacketHandler(
    ILogger<StatSkillTakeClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    ISkillMasterDataRepository skillMasterDataRepository,
    IInventoryService inventoryService,
    IDbRepository<Database.Models.Character> characterRepository,
    ICharacterMapper characterMapper)
    : IPacketHandler<StatSkillTakeClientPacket>
{
    private const int GoldItemId = 1;

    public async Task HandleAsync(PlayerState player, StatSkillTakeClientPacket packet)
    {
        if (packet.SessionId != player.SessionId)
        {
            return;
        }

        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Trainer, logger);
        if (npc is null) return;

        var skillMaster = skillMasterDataRepository.GetByBehaviorId(npc.Data.BehaviorId);
        if (skillMaster == null)
        {
            return;
        }

        var spellId = packet.SpellId;
        if (spellId <= 0)
        {
            return;
        }

        var skill = skillMaster.Skills.FirstOrDefault(s => s.SkillId == spellId);
        if (skill == null)
        {
            return;
        }

        var character = player.Character;

        // Check if player already knows this spell
        if (character.Spells.Items.Any(s => s.Id == spellId))
        {
            return;
        }

        // Check gold
        if (inventoryService.GetItemAmount(character, GoldItemId) < skill.Price)
        {
            return;
        }

        // Check level requirement
        if (skill.LevelRequirement > 0 && character.Level < skill.LevelRequirement)
        {
            return;
        }

        // Check stat requirements
        if (character.Str < skill.StrRequirement ||
            character.Int < skill.IntRequirement ||
            character.Wis < skill.WisRequirement ||
            character.Agi < skill.AgiRequirement ||
            character.Con < skill.ConRequirement ||
            character.Cha < skill.ChaRequirement)
        {
            return;
        }

        // Check skill prerequisites
        if (skill.SkillRequirements.Any(req => req > 0 && !character.Spells.Items.Any(s => s.Id == req)))
        {
            return;
        }

        // Check class requirement
        if (skill.ClassRequirement > 0 && character.Class != skill.ClassRequirement)
        {
            await player.Send(new StatSkillReplyServerPacket
            {
                ReplyCode = SkillMasterReply.WrongClass,
                ReplyCodeData = new StatSkillReplyServerPacket.ReplyCodeDataWrongClass
                {
                    ClassId = skill.ClassRequirement
                }
            });
            return;
        }

        // Take gold
        inventoryService.TryRemoveItem(character, GoldItemId, skill.Price);

        // Add spell
        character.Spells.Items.Add(new Game.Models.Spell(spellId, 0));

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        logger.LogInformation("Player {Character} learned spell {SpellId} from skill master",
            character.Name, spellId);

        await player.Send(new StatSkillTakeServerPacket
        {
            SpellId = spellId,
            GoldAmount = inventoryService.GetItemAmount(character, GoldItemId)
        });
    }
}
