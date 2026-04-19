using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of stat calculation.
///     Matches reoserv's calculate_stats: resets all derived stats, sums equipment
///     bonuses from ALL 15 slots uniformly, then applies class-based formulas.
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

        // Start with base stats + class bonuses (matches reoserv calculate_stats.rs:16-21)
        // Store on character so packet responses can send adjusted values
        character.AdjStr = character.Str + @class.Str;
        character.AdjInt = character.Int + @class.Intl;
        character.AdjWis = character.Wis + @class.Wis;
        character.AdjAgi = character.Agi + @class.Agi;
        character.AdjCon = character.Con + @class.Con;
        character.AdjCha = character.Cha + @class.Cha;

        // Reset all derived stats to 0 before recalculating (matches reoserv calculate_stats.rs:23-31)
        character.MaxHp = 0;
        character.MaxTp = 0;
        character.MaxSp = 0;
        character.MinDamage = 0;
        character.MaxDamage = 0;
        character.Accuracy = 0;
        character.Evade = 0;
        character.Armor = 0;

        // Sum ALL stats from ALL 15 equipment slots uniformly (matches reoserv calculate_stats.rs:42-80)
        var eif = _dataFileRepository.Eif;
        var equipment = new[]
        {
            character.Paperdoll.Boots,
            character.Paperdoll.Accessory,
            character.Paperdoll.Gloves,
            character.Paperdoll.Belt,
            character.Paperdoll.Armor,
            character.Paperdoll.Necklace,
            character.Paperdoll.Hat,
            character.Paperdoll.Shield,
            character.Paperdoll.Weapon,
            character.Paperdoll.Ring1,
            character.Paperdoll.Ring2,
            character.Paperdoll.Armlet1,
            character.Paperdoll.Armlet2,
            character.Paperdoll.Bracer1,
            character.Paperdoll.Bracer2
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

            // Equipment stat bonuses
            character.AdjStr += item.Str;
            character.AdjInt += item.Intl;
            character.AdjWis += item.Wis;
            character.AdjAgi += item.Agi;
            character.AdjCon += item.Con;
            character.AdjCha += item.Cha;

            // Equipment HP/TP/damage/accuracy/evade/armor from ALL slots
            character.MaxHp += item.Hp;
            character.MaxTp += item.Tp;
            character.MinDamage += item.MinDamage;
            character.MaxDamage += item.MaxDamage;
            character.Accuracy += item.Accuracy;
            character.Evade += item.Evade;
            character.Armor += item.Armor;
        }

        // Formula-based stats using adjusted values (matches reoserv formula evaluation)
        // HP formula: EOSERV default: Level / 2 + 10 + (Con * 5) + (Class.Con * Level / 10)
        character.MaxHp += character.Level / 2 + 10 + character.AdjCon * 5 + @class.Con * character.Level / 10;
        character.MaxHp = Math.Min(character.MaxHp, 32767);

        // TP formula: EOSERV default: Level + (Intl * 2) + (Class.Wis * Level / 10)
        character.MaxTp += character.Level + character.AdjInt * 2 + @class.Wis * character.Level / 10;
        character.MaxTp = Math.Min(character.MaxTp, 32767);

        // SP formula: EOSERV default: Level / 4 + 50 + (Agi * 2) + (Class.Agi * Level / 10)
        character.MaxSp += character.Level / 4 + 50 + character.AdjAgi * 2 + @class.Agi * character.Level / 10;
        character.MaxSp = Math.Min(character.MaxSp, 32767);

        // MaxWeight formula: EOSERV: 70 + (Str * 5) + (Class.Str * Level / 10)
        character.MaxWeight = 70 + character.AdjStr * 5 + @class.Str * character.Level / 10;
        character.MaxWeight = Math.Min(character.MaxWeight, 250);

        // Class-based damage formula (matches reoserv class_formulas.damage)
        // Reoserv adds the SAME damage value to both min and max
        var classDamage = @class.StatGroup switch
        {
            0 => character.AdjStr / 3,     // Melee (Str)
            1 => character.AdjStr / 5,     // Rogue (Str)
            2 => character.AdjInt / 3,     // Caster (Int)
            3 => character.AdjStr / 6,     // Archer (Str)
            _ => 0               // Peasant/Other
        };
        character.MinDamage += classDamage;
        character.MaxDamage += classDamage;

        // Class-based accuracy formula
        var classAccuracy = @class.StatGroup switch
        {
            0 => character.AdjAgi / 3,     // Melee (Agi)
            1 => character.AdjAgi / 3,     // Rogue (Agi)
            2 => character.AdjWis / 3,     // Caster (Wis)
            3 => character.AdjAgi / 5,     // Archer (Agi)
            _ => 0
        };
        character.Accuracy += classAccuracy;

        // Class-based defense/armor formula
        var classDefense = @class.StatGroup switch
        {
            0 => character.AdjCon / 4,     // Melee (Con)
            1 => character.AdjCon / 4,     // Rogue (Con)
            2 => character.AdjCon / 5,     // Caster (Con)
            3 => character.AdjCon / 5,     // Archer (Con)
            _ => 0
        };
        character.Armor += classDefense;

        // Class-based evade formula
        var classEvade = @class.StatGroup switch
        {
            0 => character.AdjAgi / 5,     // Melee (Agi)
            1 => character.AdjAgi / 3,     // Rogue (Agi)
            2 => character.AdjAgi / 4,     // Caster (Agi)
            3 => character.AdjAgi / 4,     // Archer (Agi)
            _ => 0
        };
        character.Evade += classEvade;

        // Ensure min/max damage are at least 1 (matches reoserv calculate_stats.rs:172-178)
        if (character.MinDamage == 0) character.MinDamage = 1;
        if (character.MaxDamage == 0) character.MaxDamage = 1;

        // Clamp HP/TP/SP to their new maximums (matches reoserv calculate_stats.rs:180-186)
        character.Hp = Math.Min(character.Hp, character.MaxHp);
        character.Tp = Math.Min(character.Tp, character.MaxTp);
        character.Sp = Math.Min(character.Sp, character.MaxSp);
    }
}