using System.Collections.Concurrent;
using Acorn.Game.Models;
using Acorn.Options;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of stat/skill point spending. Mirrors eoserv:
///     base stats are capped at MaxStat, skill levels at MaxSkillLevel, and a
///     reset returns Level * StatPerLevel / Level * SkillPerLevel points.
/// </summary>
public class StatSkillService(
    IStatCalculator statCalculator,
    IOptions<ServerOptions> serverOptions) : IStatSkillService
{
    private readonly ServerOptions _options = serverOptions.Value;

    public StatSkillSpendOutcome AddStatPoint(Character character, StatId statId, Ecf classes)
    {
        if (character.StatPoints <= 0)
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.NoStatPoints);
        }

        if (GetBaseStat(character, statId) >= _options.MaxStat)
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.MaxStatReached);
        }

        if (!TryIncrementBaseStat(character, statId))
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.InvalidStat);
        }

        character.StatPoints--;
        statCalculator.RecalculateStats(character, classes);

        return new StatSkillSpendOutcome(StatSkillSpendResult.Success);
    }

    public StatSkillSpendOutcome AddSkillPoint(Character character, int spellId)
    {
        if (character.SkillPoints <= 0)
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.NoSkillPoints);
        }

        var spell = character.Spells.Items.FirstOrDefault(s => s.Id == spellId);
        if (spell is null)
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.UnknownSpell);
        }

        if (spell.Level >= _options.MaxSkillLevel)
        {
            return new StatSkillSpendOutcome(StatSkillSpendResult.MaxSkillLevelReached);
        }

        var newLevel = spell.Level + 1;
        var updatedSpells = character.Spells.Items
            .Where(s => s.Id != spellId)
            .Append(new Spell(spellId, newLevel))
            .ToList();

        character.Spells = new Spells(new ConcurrentBag<Spell>(updatedSpells));
        character.SkillPoints--;

        return new StatSkillSpendOutcome(StatSkillSpendResult.Success, newLevel);
    }

    public void Reset(Character character, Ecf classes)
    {
        character.Str = 0;
        character.Int = 0;
        character.Wis = 0;
        character.Agi = 0;
        character.Con = 0;
        character.Cha = 0;

        character.Spells = new Spells(new ConcurrentBag<Spell>());

        character.StatPoints = character.Level * _options.StatPerLevel;
        character.SkillPoints = character.Level * _options.SkillPerLevel;

        statCalculator.RecalculateStats(character, classes);
    }

    private static int GetBaseStat(Character character, StatId statId)
    {
        return statId switch
        {
            StatId.Str => character.Str,
            StatId.Int => character.Int,
            StatId.Wis => character.Wis,
            StatId.Agi => character.Agi,
            StatId.Con => character.Con,
            StatId.Cha => character.Cha,
            _ => int.MinValue
        };
    }

    private static bool TryIncrementBaseStat(Character character, StatId statId)
    {
        switch (statId)
        {
            case StatId.Str:
                character.Str++;
                return true;
            case StatId.Int:
                character.Int++;
                return true;
            case StatId.Wis:
                character.Wis++;
                return true;
            case StatId.Agi:
                character.Agi++;
                return true;
            case StatId.Con:
                character.Con++;
                return true;
            case StatId.Cha:
                character.Cha++;
                return true;
            default:
                return false;
        }
    }
}
