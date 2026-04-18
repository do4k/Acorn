using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn;

/// <summary>
///     Formula calculations for game mechanics.
/// </summary>
public class FormulaService : IFormulaService
{
    private static readonly Random _random = new();
    private readonly IStatCalculator _statCalculator;

    public FormulaService(IStatCalculator statCalculator)
    {
        _statCalculator = statCalculator;
    }

    /// <summary>
    ///     Calculate hit rate for an attack.
    ///     Formula: min(0.8, max(0.5, accuracy / (target_evade * 2.0)))
    ///     If target is sitting, always hits (1.0)
    /// </summary>
    public double CalculateHitRate(int accuracy, int targetEvade, bool targetSitting = false)
    {
        if (targetSitting)
        {
            return 1.0;
        }

        if (accuracy + targetEvade == 0)
        {
            return 0.5;
        }

        return Math.Min(0.8, Math.Max(0.5, accuracy / (targetEvade * 2.0)));
    }

    /// <summary>
    ///     Determine if an attack hits based on hit rate.
    /// </summary>
    public bool DoesAttackHit(int accuracy, int targetEvade, bool targetSitting = false)
    {
        var hitRate = CalculateHitRate(accuracy, targetEvade, targetSitting);
        return _random.NextDouble() < hitRate;
    }

    /// <summary>
    ///     Calculate final damage after armor reduction.
    ///     Formula: if(critical, 1.5, 1.0) * max(1, if(damage >= target_armor * 2.0, damage, damage * pow(damage /
    ///     (target_armor * 2.0), 2.0)))
    /// </summary>
    public int CalculateDamage(int rawDamage, int targetArmor, bool critical = false)
    {
        var criticalMultiplier = critical ? 1.5 : 1.0;
        var armorThreshold = targetArmor * 2.0;

        double damage;
        if (rawDamage >= armorThreshold || targetArmor == 0)
        {
            damage = rawDamage;
        }
        else
        {
            // Armor reduction: damage * (damage / (armor * 2))^2
            var ratio = rawDamage / armorThreshold;
            damage = rawDamage * Math.Pow(ratio, 2.0);
        }

        return Math.Max(1, (int)(criticalMultiplier * damage));
    }

    /// <summary>
    ///     Calculate damage dealt to an NPC.
    /// </summary>
    public int CalculateDamageToNpc(Character character, EnfRecord npcData, bool attackingBackOrSide = false)
    {
        // Check if attack hits
        if (!DoesAttackHit(character.Accuracy, npcData.Evade))
        {
            return 0; // Miss
        }

        // Roll damage between min and max
        var rawDamage = _random.Next(character.MinDamage, character.MaxDamage + 1);

        // Critical hit if NPC is at full HP or attacking from back/side
        var critical = npcData.Hp == npcData.Hp || attackingBackOrSide;

        return CalculateDamage(rawDamage, npcData.Armor, critical);
    }

    /// <summary>
    ///     Calculate damage dealt by an NPC to a player.
    /// </summary>
    public int CalculateNpcDamageToPlayer(EnfRecord npcData, Character target, bool attackingBackOrSide = false)
    {
        // Check if attack hits
        if (!DoesAttackHit(npcData.Accuracy, target.Evade, target.SitState != SitState.Stand))
        {
            return 0; // Miss
        }

        // Roll damage between NPC's min and max
        var rawDamage = _random.Next(npcData.MinDamage, npcData.MaxDamage + 1);

        // Critical hit if NPC is attacking from back or side
        var critical = attackingBackOrSide;

        return Math.Min(CalculateDamage(rawDamage, target.Armor, critical), target.Hp);
    }

    /// <summary>
    ///     Calculate damage dealt to a player.
    /// </summary>
    public int CalculateDamageToPlayer(Character attacker, Character target, bool attackingBackOrSide = false)
    {
        // Check if attack hits
        if (!DoesAttackHit(attacker.Accuracy, target.Evade))
        {
            return 0; // Miss
        }

        // Roll damage between min and max
        var rawDamage = _random.Next(attacker.MinDamage, attacker.MaxDamage + 1);

        // Critical hit if target is at full HP or attacking from back/side
        var critical = target.Hp == target.MaxHp || attackingBackOrSide;

        return CalculateDamage(rawDamage, target.Armor, critical);
    }

    /// <summary>
    ///     Calculate max HP.
    ///     Formula: 10.0 + (2.5 * level) + (2.5 * con)
    /// </summary>
    public int CalculateMaxHp(int level, int con)
    {
        return Math.Min(64000, (int)(10.0 + 2.5 * level + 2.5 * con));
    }

    /// <summary>
    ///     Calculate max TP.
    ///     Formula: 10.0 + (2.5 * level) + (2.5 * int) + (1.5 * wis)
    /// </summary>
    public int CalculateMaxTp(int level, int intelligence, int wisdom)
    {
        return Math.Min(64000, (int)(10.0 + 2.5 * level + 2.5 * intelligence + 1.5 * wisdom));
    }

    /// <summary>
    ///     Calculate max SP.
    ///     Formula: 20.0 + (2.0 * level)
    /// </summary>
    public int CalculateMaxSp(int level)
    {
        return Math.Min(64000, (int)(20.0 + 2.0 * level));
    }

    /// <summary>
    ///     Calculate max weight.
    ///     Formula: 70.0 + str
    /// </summary>
    public int CalculateMaxWeight(int strength)
    {
        return Math.Min(250, 70 + strength);
    }

    /// <summary>
    ///     Get class-specific damage bonus.
    ///     Based on stat_group: Melee=str/3, Rogue=str/5, Caster=int/3, Archer=str/6
    /// </summary>
    public int GetClassDamageBonus(int statGroup, int strength, int intelligence)
    {
        return statGroup switch
        {
            0 => strength / 3, // Melee
            1 => strength / 5, // Rogue
            2 => intelligence / 3, // Caster
            3 => strength / 6, // Archer
            _ => 0 // Peasant
        };
    }

    /// <summary>
    ///     Get class-specific accuracy bonus.
    ///     Based on stat_group: Melee=agi/3, Rogue=agi/3, Caster=wis/3, Archer=agi/5
    /// </summary>
    public int GetClassAccuracyBonus(int statGroup, int agility, int wisdom)
    {
        return statGroup switch
        {
            0 => agility / 3, // Melee
            1 => agility / 3, // Rogue
            2 => wisdom / 3, // Caster
            3 => agility / 5, // Archer
            _ => 0 // Peasant
        };
    }

    /// <summary>
    ///     Get class-specific evade bonus.
    ///     Based on stat_group: Melee=agi/5, Rogue=agi/3, Caster=agi/4, Archer=agi/4
    /// </summary>
    public int GetClassEvadeBonus(int statGroup, int agility)
    {
        return statGroup switch
        {
            0 => agility / 5, // Melee
            1 => agility / 3, // Rogue
            2 => agility / 4, // Caster
            3 => agility / 4, // Archer
            _ => 0 // Peasant
        };
    }

    /// <summary>
    ///     Get class-specific defense bonus.
    ///     Based on stat_group: Melee=con/4, Rogue=con/4, Caster=con/5, Archer=con/5
    /// </summary>
    public int GetClassDefenseBonus(int statGroup, int constitution)
    {
        return statGroup switch
        {
            0 => constitution / 4, // Melee
            1 => constitution / 4, // Rogue
            2 => constitution / 5, // Caster
            3 => constitution / 5, // Archer
            _ => 0 // Peasant
        };
    }

    /// <summary>
    ///     Calculate party experience share.
    ///     Formula: if(members > 2, floor(exp * ((1 + members) / members)), floor(exp / 2))
    /// </summary>
    public int CalculatePartyExpShare(int exp, int members)
    {
        if (members <= 1)
        {
            return exp;
        }

        if (members > 2)
        {
            return (int)Math.Floor(exp * ((1.0 + members) / members));
        }

        return (int)Math.Floor(exp / 2.0);
    }

    /// <summary>
    ///     Get experience from NPC data. In EO, experience comes directly from EnfRecord.Experience.
    ///     This is a simple passthrough for clarity.
    /// </summary>
    public int GetNpcExperience(EnfRecord npcData)
    {
        return Math.Max(0, npcData.Experience);
    }

    /// <summary>
    ///     Calculate the CUMULATIVE experience required to reach a level.
    ///     Formula: (level^3 * 133.1).Round()
    ///     This is the total experience from level 0, NOT the difference between levels.
    /// </summary>
    public int GetCumulativeExperienceForLevel(int level)
    {
        if (level <= 0)
        {
            return 0;
        }

        if (level >= 250)
        {
            return int.MaxValue; // Max level cap
        }

        return (int)Math.Round(Math.Pow(level, 3) * 133.1);
    }

    /// <summary>
    ///     Check if character has enough experience to level up.
    /// </summary>
    public bool CanLevelUp(Character character)
    {
        if (character.Level >= 250)
        {
            return false;
        }

        var nextLevelThreshold = GetCumulativeExperienceForLevel(character.Level + 1);
        return character.Exp >= nextLevelThreshold;
    }

    /// <summary>
    ///     Level up a character and return the new level.
    /// </summary>
    public int LevelUp(Character character, Ecf classes)
    {
        if (!CanLevelUp(character))
        {
            return character.Level;
        }

        var @class = classes.GetClass(character.Class);
        if (@class is null)
        {
            return character.Level;
        }

        // Increment level
        character.Level++;

        // Three stat points per level
        character.StatPoints += 3;

        // One skill point per level
        character.SkillPoints += 1;

        // Recalculate stats
        _statCalculator.RecalculateStats(character, classes);

        // Fully restore HP/TP/SP on level up
        character.Hp = character.MaxHp;
        character.Tp = character.MaxTp;
        character.Sp = character.MaxSp;

        return character.Level;
    }
}