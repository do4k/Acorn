using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
/// Default implementation of stat calculation based on EOSERV formulas.
/// </summary>
public class StatCalculator : IStatCalculator
{
    public void RecalculateStats(Character character, Ecf classes)
    {
        var @class = classes.GetClass(character.Class);
        if (@class is null)
        {
            return;
        }

        // Calculate adjusted stats from equipment bonuses
        // TODO: Add equipment stat bonuses when paperdoll items have stats
        int adjStr = character.Str;
        int adjIntl = character.Wis; // Note: SDK uses Wis for Int
        int adjWis = character.Wis;
        int adjAgi = character.Agi;
        int adjCon = character.Con;
        int adjCha = character.Cha;

        // Base MaxHP: (level * con / 20) + (level * class_con / 10) + base
        // EOSERV default: Level / 2 + 10 + (Con * 5) + (Class.Con * Level / 10)
        character.MaxHp = (character.Level / 2) + 10 + (adjCon * 5) + (@class.Con * character.Level / 10);
        character.MaxHp = Math.Min(character.MaxHp, 32767); // Short max value

        // Base MaxTP: (level * int / 20) + (level * class_int / 10) + base
        // EOSERV default: Level + (Wis * 2) + (Class.Wis * Level / 10)
        character.MaxTp = character.Level + (adjIntl * 2) + (@class.Wis * character.Level / 10);
        character.MaxTp = Math.Min(character.MaxTp, 32767);

        // Base MaxSP: (level * agi / 20) + (level * class_agi / 10) + base  
        // EOSERV default: Level / 4 + 50 + (Agi * 2) + (Class.Agi * Level / 10)
        character.MaxSp = (character.Level / 4) + 50 + (adjAgi * 2) + (@class.Agi * character.Level / 10);
        character.MaxSp = Math.Min(character.MaxSp, 32767);

        // Calculate MaxWeight
        // EOSERV: 70 + (Str * 5) + (Class.Str * Level / 10)
        character.MaxWeight = 70 + (adjStr * 5) + (@class.Str * character.Level / 10);
        character.MaxWeight = Math.Min(character.MaxWeight, 250);

        // Calculate base damage (before equipment)
        // EOSERV uses class-specific formulas based on StatGroup
        int baseDam = @class.StatGroup switch
        {
            1 => adjStr / 5,        // Warriors (Str)
            2 => adjAgi / 5,        // Rogues (Agi)
            3 => adjIntl / 5,       // Mages (Int)
            4 => adjAgi / 5,        // Archers (Agi)
            _ => (adjStr + adjAgi + adjIntl) / 15  // Balanced
        };

        character.MinDamage = 1 + baseDam;
        character.MaxDamage = 2 + baseDam + (character.Level / 10);

        // TODO: Add equipment damage bonuses

        character.MinDamage = Math.Min(character.MinDamage, 32767);
        character.MaxDamage = Math.Min(character.MaxDamage, 32767);

        // Calculate Accuracy (Agi-based + class bonus + level)
        character.Accuracy = (adjAgi / 2) + (@class.Agi / 4) + character.Level;
        // TODO: Add weapon accuracy bonus
        
        // Calculate Evade (Agi-based + class bonus)
        character.Evade = (adjAgi / 2) + (@class.Agi / 4);
        // TODO: Add armor evade bonus
        
        // Calculate Armor (Con-based)
        character.Armor = adjCon / 4;
        // TODO: Add equipment armor bonuses

        character.Accuracy = Math.Min(character.Accuracy, 32767);
        character.Evade = Math.Min(character.Evade, 32767);
        character.Armor = Math.Min(character.Armor, 32767);

        // Clamp HP/TP/SP to their new maximums
        character.Hp = Math.Min(character.Hp, character.MaxHp);
        character.Tp = Math.Min(character.Tp, character.MaxTp);
        character.Sp = Math.Min(character.Sp, character.MaxSp);
    }
}
