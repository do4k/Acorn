using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Net.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
internal class StatSkillAddClientPacketHandler(
    IDbRepository<Database.Models.Character> characterRepository,
    IStatSkillService statSkillService,
    ICharacterMapper characterMapper,
    IDataFileRepository dataFileRepository,
    INotificationService notificationService,
    ILogger<StatSkillAddClientPacketHandler> logger)
    : IPacketHandler<StatSkillAddClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, StatSkillAddClientPacket packet)
    {
        switch (packet.ActionTypeData)
        {
            case StatSkillAddClientPacket.ActionTypeDataStat statData:
                await HandleStatIncrease(playerState, statData.StatId);
                break;
            case StatSkillAddClientPacket.ActionTypeDataSkill skillData:
                await HandleSkillIncrease(playerState, skillData.SpellId);
                break;
            default:
                logger.LogWarning("Unknown StatSkillAdd action type");
                break;
        }
    }

    private async Task HandleStatIncrease(PlayerState playerState, StatId statId)
    {
        var character = playerState.Character!;

        var outcome = statSkillService.AddStatPoint(character, statId, dataFileRepository.Ecf);
        if (!outcome.Success)
        {
            await notificationService.SystemMessage(playerState, DescribeFailure(outcome.Result, statId));
            return;
        }

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        // Send updated stats to client
        await playerState.Send(StatSkillPacketMapper.ToStatPlayer(character));

        logger.LogInformation("Character '{CharacterName}' increased stat {StatId}", character.Name, statId);
    }

    private async Task HandleSkillIncrease(PlayerState playerState, int spellId)
    {
        var character = playerState.Character!;

        var outcome = statSkillService.AddSkillPoint(character, spellId);
        if (!outcome.Success)
        {
            await notificationService.SystemMessage(playerState, DescribeFailure(outcome.Result, null));
            return;
        }

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        // Skill spends must use StatSkill/Accept (skill points + spell id + level),
        // not StatSkill/Player, or the client never updates the skill.
        await playerState.Send(
            StatSkillPacketMapper.ToSkillAccept(character.SkillPoints, spellId, outcome.SkillLevel));

        logger.LogInformation("Character '{CharacterName}' increased spell {SpellId} to level {Level}",
            character.Name, spellId, outcome.SkillLevel);
    }

    private static string DescribeFailure(StatSkillSpendResult result, StatId? statId)
    {
        return result switch
        {
            StatSkillSpendResult.NoStatPoints => "You don't have any stat points to spend.",
            StatSkillSpendResult.NoSkillPoints => "You don't have any skill points to spend.",
            StatSkillSpendResult.MaxStatReached => "That stat is already at its maximum value.",
            StatSkillSpendResult.MaxSkillLevelReached => "That skill is already at its maximum level.",
            StatSkillSpendResult.UnknownSpell => "You don't know that spell.",
            StatSkillSpendResult.InvalidStat => $"Invalid stat ID: {statId}",
            _ => "Unable to spend that point."
        };
    }
}
