using Acorn.Database.Repository;
using Acorn.Domain.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of stat calculation based on EOSERV formulas.
/// </summary>
public class StatCalculator : IStatCalculator
{
    private readonly IDataFileRepository _dataFileRepository;

    public StatCalculator(IDataFileRepository dataFileRepository)
    {
        _dataFileRepository = dataFileRepository;
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

        // Base MaxHP: (level * con / 20) + (level * class_con / 10) + base
        // EOSERV default: Level / 2 + 10 + (Con * 5) + (Class.Con * Level / 10)
        character.MaxHp = character.Level / 2 + 10 + adjCon * 5 + @class.Con * character.Level / 10;
        character.MaxHp = Math.Min(character.MaxHp, 32767); // Short max value

        // Base MaxTP: (level * int / 20) + (level * class_int / 10) + base
        // EOSERV default: Level + (Intl * 2) + (Class.Wis * Level / 10)
        character.MaxTp = character.Level + adjIntl * 2 + @class.Wis * character.Level / 10;
        character.MaxTp = Math.Min(character.MaxTp, 32767);

        // Base MaxSP: (level * agi / 20) + (level * class_agi / 10) + base  
        // EOSERV default: Level / 4 + 50 + (Agi * 2) + (Class.Agi * Level / 10)
        character.MaxSp = character.Level / 4 + 50 + adjAgi * 2 + @class.Agi * character.Level / 10;
        character.MaxSp = Math.Min(character.MaxSp, 32767);

        // Calculate MaxWeight
        // EOSERV: 70 + (Str * 5) + (Class.Str * Level / 10)
        character.MaxWeight = 70 + adjStr * 5 + @class.Str * character.Level / 10;
        character.MaxWeight = Math.Min(character.MaxWeight, 250);

        // Calculate base damage (Str/Int based on class stat group)
        // Formulas copied from eoserv: https://github.com/eoserv/mainclone-eoserv/blob/main/data/formulas.ini
        var baseDam = @class.StatGroup switch
        {
            0 => adjStr / 3, // Melee (Str)
            1 => adjStr / 5, // Rogue (Str) 
            2 => adjIntl / 3, // Caster (Int)
            3 => adjStr / 6, // Archer (Str)
            _ => 0 // Peasant/Other
        };

        var minDamage = Math.Max(1, baseDam);
        var maxDamage = Math.Max(1, baseDam + character.Level / 10);

        // Add equipment damage bonuses (from weapon, etc.)
        AddEquipmentDamageBonuses(character, ref minDamage, ref maxDamage);

        character.MinDamage = Math.Min(minDamage, 32767);
        character.MaxDamage = Math.Min(maxDamage, 32767);

        // Calculate Accuracy (based on class stat group)
        // Formulas: Melee=agi/3, Rogue=agi/3, Caster=wis/3, Archer=agi/5
        var baseAccuracy = @class.StatGroup switch
        {
            0 => adjAgi / 3, // Melee (Agi)
            1 => adjAgi / 3, // Rogue (Agi)
            2 => adjWis / 3, // Caster (Wis)
            3 => adjAgi / 5, // Archer (Agi)
            _ => 0 // Peasant/Other
        };
        var accuracy = baseAccuracy;
        // Add weapon accuracy bonus
        AddEquipmentAccuracyBonus(character, ref accuracy);
        character.Accuracy = Math.Min(accuracy, 32767);

        // Calculate Evade (based on class stat group)
        // Formulas: Melee=agi/5, Rogue=agi/3, Caster=agi/4, Archer=agi/4
        var baseEvade = @class.StatGroup switch
        {
            0 => adjAgi / 5, // Melee (Agi)
            1 => adjAgi / 3, // Rogue (Agi)
            2 => adjAgi / 4, // Caster (Agi)
            3 => adjAgi / 4, // Archer (Agi)
            _ => 0 // Peasant/Other
        };
        var evade = baseEvade;
        // Add armor evade bonus
        AddEquipmentEvadeBonus(character, ref evade);
        character.Evade = Math.Min(evade, 32767);

        // Calculate Armor (based on class stat group)
        // Formulas: Melee=con/4, Rogue=con/4, Caster=con/5, Archer=con/5
        var baseArmor = @class.StatGroup switch
        {
            0 => adjCon / 4, // Melee (Con)
            1 => adjCon / 4, // Rogue (Con)
            2 => adjCon / 5, // Caster (Con)
            3 => adjCon / 5, // Archer (Con)
            _ => 0 // Peasant/Other
        };
        var armor = baseArmor;
        // Add equipment armor bonuses
        AddEquipmentArmorBonus(character, ref armor);
        character.Armor = Math.Min(armor, 32767);

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