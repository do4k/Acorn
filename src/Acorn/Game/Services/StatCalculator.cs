using Acorn.Database.Repository;
using Acorn.Domain.Models;
using Acorn.Options;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of stat calculation using configurable formulas (reoserv-style).
/// </summary>
public class StatCalculator : IStatCalculator
{
    private readonly IDataFileRepository _dataFileRepository;
    private readonly FormulasOptions _formulas;

    public StatCalculator(IDataFileRepository dataFileRepository, IOptions<ServerOptions> serverOptions)
    {
        _dataFileRepository = dataFileRepository;
        _formulas = serverOptions.Value.Formulas;
    }

    public void RecalculateStats(Character character, Ecf classes)
    {
        var @class = classes.GetClass(character.Class);
        if (@class is null)
        {
            return;
        }

        // Calculate adjusted stats: base stats + class bonuses + equipment bonuses
        var adjStr = character.Str + @class.Str;
        var adjIntl = character.Int + @class.Intl;
        var adjWis = character.Wis + @class.Wis;
        var adjAgi = character.Agi + @class.Agi;
        var adjCon = character.Con + @class.Con;
        var adjCha = character.Cha + @class.Cha;

        // Add equipment bonuses
        AddEquipmentBonuses(character, ref adjStr, ref adjIntl, ref adjWis, ref adjAgi, ref adjCon, ref adjCha);

        // reoserv formulas: MaxHP = base + (level_mult * level) + (stat_mult * con)
        character.MaxHp = (int)(_formulas.MaxHpBase +
                                (_formulas.MaxHpLevelMultiplier * character.Level) +
                                (_formulas.MaxHpStatMultiplier * adjCon));
        character.MaxHp = Math.Min(character.MaxHp, _formulas.StatCap);

        // reoserv formulas: MaxTP = base + (level_mult * level) + (int_mult * int) + (wis_mult * wis)
        character.MaxTp = (int)(_formulas.MaxTpBase +
                                (_formulas.MaxTpLevelMultiplier * character.Level) +
                                (_formulas.MaxTpIntMultiplier * adjIntl) +
                                (_formulas.MaxTpWisMultiplier * adjWis));
        character.MaxTp = Math.Min(character.MaxTp, _formulas.StatCap);

        // reoserv formulas: MaxSP = base + (level_mult * level)
        character.MaxSp = (int)(_formulas.MaxSpBase + (_formulas.MaxSpLevelMultiplier * character.Level));
        character.MaxSp = Math.Min(character.MaxSp, _formulas.StatCap);

        // Calculate MaxWeight: base + str (no multiplier)
        character.MaxWeight = _formulas.MaxWeightBase + adjStr;
        character.MaxWeight = Math.Min(character.MaxWeight, _formulas.MaxWeightCap);

        // Calculate base damage (Str/Int based on class stat group)
        // Uses configurable divisors per stat group
        var baseDam = @class.StatGroup switch
        {
            0 => adjStr / _formulas.MeleeDamageDivisor, // Melee (Str)
            1 => adjStr / _formulas.RogueDamageDivisor, // Rogue (Str) 
            2 => adjIntl / _formulas.CasterDamageDivisor, // Caster (Int)
            3 => adjStr / _formulas.ArcherDamageDivisor, // Archer (Str)
            _ => 0 // Peasant/Other
        };

        // MinDamage and MaxDamage start the same (equipment adds difference)
        var minDamage = Math.Max(1, baseDam);
        var maxDamage = Math.Max(1, baseDam);

        // Add equipment damage bonuses (from weapon, etc.)
        AddEquipmentDamageBonuses(character, ref minDamage, ref maxDamage);

        character.MinDamage = Math.Min(minDamage, _formulas.StatCap);
        character.MaxDamage = Math.Min(maxDamage, _formulas.StatCap);

        // Calculate Accuracy (based on class stat group) - uses configurable divisors
        var baseAccuracy = @class.StatGroup switch
        {
            0 => adjAgi / _formulas.MeleeAccuracyDivisor, // Melee (Agi)
            1 => adjAgi / _formulas.RogueAccuracyDivisor, // Rogue (Agi)
            2 => adjWis / _formulas.CasterAccuracyDivisor, // Caster (Wis)
            3 => adjAgi / _formulas.ArcherAccuracyDivisor, // Archer (Agi)
            _ => 0 // Peasant/Other
        };
        var accuracy = baseAccuracy;
        // Add weapon accuracy bonus
        AddEquipmentAccuracyBonus(character, ref accuracy);
        character.Accuracy = Math.Min(accuracy, _formulas.StatCap);

        // Calculate Evade (based on class stat group) - uses configurable divisors
        var baseEvade = @class.StatGroup switch
        {
            0 => adjAgi / _formulas.MeleeEvadeDivisor, // Melee (Agi)
            1 => adjAgi / _formulas.RogueEvadeDivisor, // Rogue (Agi)
            2 => adjAgi / _formulas.CasterEvadeDivisor, // Caster (Agi)
            3 => adjAgi / _formulas.ArcherEvadeDivisor, // Archer (Agi)
            _ => 0 // Peasant/Other
        };
        var evade = baseEvade;
        // Add armor evade bonus
        AddEquipmentEvadeBonus(character, ref evade);
        character.Evade = Math.Min(evade, _formulas.StatCap);

        // Calculate Armor (based on class stat group) - uses configurable divisors
        var baseArmor = @class.StatGroup switch
        {
            0 => adjCon / _formulas.MeleeArmorDivisor, // Melee (Con)
            1 => adjCon / _formulas.RogueArmorDivisor, // Rogue (Con)
            2 => adjCon / _formulas.CasterArmorDivisor, // Caster (Con)
            3 => adjCon / _formulas.ArcherArmorDivisor, // Archer (Con)
            _ => 0 // Peasant/Other
        };
        var armor = baseArmor;
        // Add equipment armor bonuses
        AddEquipmentArmorBonus(character, ref armor);
        character.Armor = Math.Min(armor, _formulas.StatCap);

        // Clamp HP/TP/SP to their new maximums
        character.Hp = Math.Min(character.Hp, character.MaxHp);
        character.Tp = Math.Min(character.Tp, character.MaxTp);
        character.Sp = Math.Min(character.Sp, character.MaxSp);
    }

    private void AddEquipmentBonuses(
        Character character,
        ref int adjStr, ref int adjIntl, ref int adjWis,
        ref int adjAgi, ref int adjCon, ref int adjCha)
    {
        var eif = _dataFileRepository.Eif;
        var equipment = new[]
        {
            character.Paperdoll.Hat,
            character.Paperdoll.Necklace,
            character.Paperdoll.Armor,
            character.Paperdoll.Belt,
            character.Paperdoll.Boots,
            character.Paperdoll.Gloves,
            character.Paperdoll.Weapon,
            character.Paperdoll.Shield,
            character.Paperdoll.Accessory,
            character.Paperdoll.Ring1,
            character.Paperdoll.Ring2,
            character.Paperdoll.Bracer1,
            character.Paperdoll.Bracer2,
            character.Paperdoll.Armlet1,
            character.Paperdoll.Armlet2
        };

        foreach (var itemId in equipment)
        {
            if (itemId == 0)
            {
                continue;
            }

            var item = eif.GetItem(itemId);
            if (item is null)
            {
                continue;
            }

            adjStr += item.Str;
            adjIntl += item.Intl;
            adjWis += item.Wis;
            adjAgi += item.Agi;
            adjCon += item.Con;
            adjCha += item.Cha;

            // Add HP/TP bonuses from equipment
            character.MaxHp += item.Hp;
            character.MaxTp += item.Tp;
        }
    }

    private void AddEquipmentDamageBonuses(Character character, ref int minDamage, ref int maxDamage)
    {
        var eif = _dataFileRepository.Eif;
        var equipment = new[]
        {
            character.Paperdoll.Weapon,
            character.Paperdoll.Shield,
            character.Paperdoll.Accessory,
            character.Paperdoll.Ring1,
            character.Paperdoll.Ring2,
            character.Paperdoll.Bracer1,
            character.Paperdoll.Bracer2,
            character.Paperdoll.Armlet1,
            character.Paperdoll.Armlet2
        };

        foreach (var itemId in equipment)
        {
            if (itemId == 0)
            {
                continue;
            }

            var item = eif.GetItem(itemId);
            if (item is null)
            {
                continue;
            }

            minDamage += item.MinDamage;
            maxDamage += item.MaxDamage;
        }
    }

    private void AddEquipmentAccuracyBonus(Character character, ref int accuracy)
    {
        var eif = _dataFileRepository.Eif;
        var weapon = character.Paperdoll.Weapon;

        if (weapon == 0)
        {
            return;
        }

        var item = eif.GetItem(weapon);
        if (item is not null)
        {
            accuracy += item.Accuracy;
        }
    }

    private void AddEquipmentEvadeBonus(Character character, ref int evade)
    {
        var eif = _dataFileRepository.Eif;
        var armor = character.Paperdoll.Armor;

        if (armor == 0)
        {
            return;
        }

        var item = eif.GetItem(armor);
        if (item is not null)
        {
            evade += item.Evade;
        }
    }

    private void AddEquipmentArmorBonus(Character character, ref int armor)
    {
        var eif = _dataFileRepository.Eif;
        var equipment = new[]
        {
            character.Paperdoll.Armor,
            character.Paperdoll.Boots,
            character.Paperdoll.Gloves,
            character.Paperdoll.Belt,
            character.Paperdoll.Hat
        };

        foreach (var itemId in equipment)
        {
            if (itemId == 0)
            {
                continue;
            }

            var item = eif.GetItem(itemId);
            if (item is null)
            {
                continue;
            }

            armor += item.Armor;
        }
    }
}