using Acorn.Database.Models;
using Acorn.Database.Repository;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn;

public class FormulaService
{
    private readonly IDataFileRepository _dataFileRepository;
    private static readonly Random _random = new Random();

    public FormulaService(IDataFileRepository dataFileRepository)
    {
        _dataFileRepository = dataFileRepository;
    }

    public int CalculateDamage(Game.Models.Character character, EnfRecord npcData)
    {
        var @class = _dataFileRepository.Ecf.GetClass(character.Class);
        if (@class is null)
        {
            return 0;
        }

        // Get weapon damage if equipped
        int weaponDamage = 0;
        if (character.Paperdoll.Weapon > 0)
        {
            var weapon = _dataFileRepository.Eif.GetItem(character.Paperdoll.Weapon);
            if (weapon != null)
            {
                // TODO: Determine correct property names for weapon damage in EifRecord
                // Likely candidates: MinDamage/MaxDamage, Str, or other stat bonuses
                weaponDamage = 0; // (weapon.MinDam + weapon.MaxDam) / 2;
            }
        }

        // Calculate base damage using character's min/max damage (which includes stats)
        int baseDamage = _random.Next(character.MinDamage, character.MaxDamage + 1) + weaponDamage;

        // Apply class-specific damage multipliers
        double classMultiplier = @class.StatGroup switch
        {
            1 => 1.0,   // Warriors - normal damage
            2 => 0.9,   // Rogues - slightly less damage
            3 => 0.8,   // Mages - less physical damage
            4 => 0.95,  // Archers - slightly less damage
            _ => 1.0    // Default
        };

        baseDamage = (int)(baseDamage * classMultiplier);

        // Apply NPC defense
        int npcDefense = npcData.Armor;
        int finalDamage = baseDamage - (npcDefense / 2);

        // Add randomness variance (±10%)
        int variance = _random.Next(-finalDamage / 10, finalDamage / 10);
        finalDamage += variance;

        // Ensure damage is at least 1 if the attack succeeded
        return Math.Max(finalDamage, 1);
    }

    public int CalculateExperience(int npcLevel, int characterLevel)
    {
        // EOSERV formula: base_exp * (npc_level / char_level) with level difference modifiers
        int baseExp = npcLevel * 10;
        
        int levelDiff = npcLevel - characterLevel;
        double modifier = levelDiff switch
        {
            > 10 => 1.5,   // Much harder mob
            > 5 => 1.2,    // Harder mob
            > 0 => 1.0,    // Slightly harder
            0 => 0.9,      // Same level
            > -5 => 0.7,   // Slightly easier
            > -10 => 0.5,  // Easier
            _ => 0.3       // Much easier
        };

        return Math.Max(1, (int)(baseExp * modifier));
    }

    public int CalculateAccuracy(Game.Models.Character character)
    {
        var @class = _dataFileRepository.Ecf.GetClass(character.Class);
        if (@class is null) return 0;

        // EOSERV: base accuracy from Agi + class bonus + level
        int accuracy = character.Agi / 2 + @class.Agi / 4 + character.Level;
        
        // TODO: Add weapon accuracy bonus
        
        return accuracy;
    }

    public int CalculateEvade(Game.Models.Character character)
    {
        var @class = _dataFileRepository.Ecf.GetClass(character.Class);
        if (@class is null) return 0;

        // EOSERV: base evade from Agi + class bonus
        int evade = character.Agi / 2 + @class.Agi / 4;
        
        // TODO: Add armor evade bonus
        
        return evade;
    }

    public int CalculateArmor(Game.Models.Character character)
    {
        var @class = _dataFileRepository.Ecf.GetClass(character.Class);
        if (@class is null) return 0;

        // Base armor from Con
        int armor = character.Con / 4;
        
        // Add equipment armor
        if (character.Paperdoll.Armor > 0)
        {
            var armorItem = _dataFileRepository.Eif.GetItem(character.Paperdoll.Armor);
            if (armorItem != null)
            {
                armor += armorItem.Hp; // Armor value is stored in HP field for armor items
            }
        }
        
        return armor;
    }

    /// <summary>
    /// Calculate the experience required to reach the next level
    /// Based on EOSERV formula: (level^3 * 133) / 100
    /// </summary>
    public int GetExperienceToNextLevel(int level)
    {
        if (level >= 250) return int.MaxValue; // Max level cap
        return (int)Math.Pow(level, 3) * 133 / 100;
    }

    /// <summary>
    /// Check if character has enough experience to level up
    /// </summary>
    public bool CanLevelUp(Game.Models.Character character)
    {
        if (character.Level >= 250) return false;
        int requiredExp = GetExperienceToNextLevel(character.Level);
        return character.Exp >= requiredExp;
    }

    /// <summary>
    /// Level up a character and return the new level
    /// </summary>
    public int LevelUp(Game.Models.Character character, Ecf classes)
    {
        if (!CanLevelUp(character)) return character.Level;

        var @class = classes.GetClass(character.Class);
        if (@class is null) return character.Level;

        // Increment level
        character.Level++;
        
        // Deduct experience for this level
        int requiredExp = GetExperienceToNextLevel(character.Level - 1);
        character.Exp -= requiredExp;

        // Three skill points per level
        character.StatPoints += 3;

        // One skill point per level
        character.SkillPoints += 1;

        // Recalculate stats
        character.CalculateStats(classes);

        // Fully restore HP/TP/SP on level up
        character.Hp = character.MaxHp;
        character.Tp = character.MaxTp;
        character.Sp = character.MaxSp;

        return character.Level;
    }
}