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

        var character = player.Character!;

        // Validate every requirement (already known, gold, level, stats, prerequisites,
        // class). Failures are no longer silent: the client expects a Reply packet.
        var validation = SkillLearnValidator.Validate(
            character, skill, inventoryService.GetItemAmount(character, GoldItemId));

        if (!validation.CanLearn)
        {
            // Already knowing the spell is a no-op, not a user-facing failure.
            if (validation.Failure != SkillLearnFailure.AlreadyKnown)
            {
                await player.Send(StatSkillPacketMapper.ToWrongClassReply(validation.WrongClassId));
            }

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
