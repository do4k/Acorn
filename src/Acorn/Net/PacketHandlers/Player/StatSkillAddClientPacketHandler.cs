using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Net.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class StatSkillAddClientPacketHandler(
    IDbRepository<Database.Models.Character> characterRepository,
    IStatCalculator statCalculator,
    ICharacterMapper characterMapper,
    IDataFileRepository dataFileRepository,
    INotificationService notificationService,
    ILogger<StatSkillAddClientPacketHandler> logger)
    : IPacketHandler<StatSkillAddClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, StatSkillAddClientPacket packet)
    {
        if (playerState.Character is null)
        {
            logger.LogWarning("StatSkillAdd packet received but player has no character loaded");
            return;
        }

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

        // Check if player has stat points
        if (character.StatPoints <= 0)
        {
            await notificationService.SystemMessage(playerState, "You don't have any stat points to spend.");
            return;
        }

        // Apply stat increase based on statId
        // StatId mapping: 1=Str, 2=Int, 3=Wis, 4=Agi, 5=Con, 6=Cha
        var statIncreased = statId switch
        {
            StatId.Str => IncreaseStat(() => character.Str++, "Strength"),
            StatId.Int => IncreaseStat(() => character.Int++, "Intelligence"),
            StatId.Wis => IncreaseStat(() => character.Wis++, "Wisdom"),
            StatId.Agi => IncreaseStat(() => character.Agi++, "Agility"),
            StatId.Con => IncreaseStat(() => character.Con++, "Constitution"),
            StatId.Cha => IncreaseStat(() => character.Cha++, "Charisma"),
            _ => false
        };

        if (!statIncreased)
        {
            await notificationService.SystemMessage(playerState, $"Invalid stat ID: {statId}");
            return;
        }

        // Deduct stat point
        character.StatPoints--;

        // Recalculate secondary stats
        statCalculator.RecalculateStats(character, dataFileRepository.Ecf);

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        // Send updated stats to client
        await SendStatUpdate(playerState, character);

        logger.LogInformation("Character '{CharacterName}' increased stat {StatId}", character.Name, statId);
    }

    private async Task HandleSkillIncrease(PlayerState playerState, int spellId)
    {
        var character = playerState.Character!;

        // Check if player has skill points
        if (character.SkillPoints <= 0)
        {
            await notificationService.SystemMessage(playerState, "You don't have any skill points to spend.");
            return;
        }

        // Find the spell in character's spell list
        var spell = character.Spells.Items.FirstOrDefault(s => s.Id == spellId);
        if (spell is null)
        {
            await notificationService.SystemMessage(playerState, $"You don't know spell {spellId}.");
            return;
        }

        // Check if already at max level (assuming max level is in ESF data)
        var spellData = dataFileRepository.Esf.GetSkill(spellId);
        if (spellData is null)
        {
            logger.LogWarning("Spell {SpellId} not found in ESF", spellId);
            return;
        }

        // Remove old spell and add updated spell (Spell is a record with init-only properties)
        // Create new Spells collection with updated spell
        var updatedSpells = character.Spells.Items
            .Where(s => s.Id != spellId)
            .Append(new Game.Models.Spell(spellId, spell.Level + 1))
            .ToList();
        
        character.Spells = new Game.Models.Spells(new System.Collections.Concurrent.ConcurrentBag<Game.Models.Spell>(updatedSpells));
        character.SkillPoints--;

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        // Send updated stats to client
        await SendStatUpdate(playerState, character);

        logger.LogInformation("Character '{CharacterName}' increased spell {SpellId} to level {Level}", 
            character.Name, spellId, spell.Level);
    }

    private bool IncreaseStat(Action increase, string statName)
    {
        increase();
        return true;
    }

    private async Task SendStatUpdate(PlayerState playerState, Game.Models.Character character)
    {
        await playerState.Send(new StatSkillPlayerServerPacket
        {
            StatPoints = character.StatPoints,
            Stats = new CharacterStatsUpdate
            {
                MaxHp = character.MaxHp,
                MaxTp = character.MaxTp,
                MaxSp = character.MaxSp,
                BaseStats = new CharacterBaseStats
                {
                    Str = character.Str,
                    Intl = character.Int,
                    Wis = character.Wis,
                    Agi = character.Agi,
                    Con = character.Con,
                    Cha = character.Cha
                },
                SecondaryStats = new CharacterSecondaryStats
                {
                    MinDamage = character.MinDamage,
                    MaxDamage = character.MaxDamage,
                    Accuracy = character.Accuracy,
                    Evade = character.Evade,
                    Armor = character.Armor
                }
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (StatSkillAddClientPacket)packet);
    }
}
