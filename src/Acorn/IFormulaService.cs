using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn;

/// <summary>
/// Service for combat and stat formula calculations.
/// </summary>
public interface IFormulaService
{
    /// <summary>
    /// Calculate hit rate for an attack.
    /// </summary>
    double CalculateHitRate(int accuracy, int targetEvade, bool targetSitting = false);

    /// <summary>
    /// Determine if an attack hits based on hit rate.
    /// </summary>
    bool DoesAttackHit(int accuracy, int targetEvade, bool targetSitting = false);

    /// <summary>
    /// Calculate final damage after armor reduction.
    /// </summary>
    int CalculateDamage(int rawDamage, int targetArmor, bool critical = false);

    /// <summary>
    /// Calculate damage dealt to an NPC.
    /// </summary>
    int CalculateDamageToNpc(Character character, EnfRecord npcData, bool attackingBackOrSide = false);

    /// <summary>
    /// Calculate damage dealt by an NPC to a player.
    /// </summary>
    int CalculateNpcDamageToPlayer(EnfRecord npcData, Character target, bool attackingBackOrSide = false);

    /// <summary>
    /// Calculate damage dealt to a player.
    /// </summary>
    int CalculateDamageToPlayer(Character attacker, Character target, bool attackingBackOrSide = false);

    /// <summary>
    /// Calculate max HP.
    /// </summary>
    int CalculateMaxHp(int level, int con);

    /// <summary>
    /// Calculate max TP.
    /// </summary>
    int CalculateMaxTp(int level, int intelligence, int wisdom);

    /// <summary>
    /// Calculate max SP.
    /// </summary>
    int CalculateMaxSp(int level);

    /// <summary>
    /// Calculate max weight.
    /// </summary>
    int CalculateMaxWeight(int strength);

    /// <summary>
    /// Get class-specific damage bonus.
    /// </summary>
    int GetClassDamageBonus(int statGroup, int strength, int intelligence);

    /// <summary>
    /// Get class-specific accuracy bonus.
    /// </summary>
    int GetClassAccuracyBonus(int statGroup, int agility, int wisdom);

    /// <summary>
    /// Get class-specific evade bonus.
    /// </summary>
    int GetClassEvadeBonus(int statGroup, int agility);

    /// <summary>
    /// Get class-specific defense bonus.
    /// </summary>
    int GetClassDefenseBonus(int statGroup, int constitution);

    /// <summary>
    /// Calculate party experience share.
    /// </summary>
    int CalculatePartyExpShare(int exp, int members);

    /// <summary>
    /// Get experience from NPC data.
    /// </summary>
    int GetNpcExperience(EnfRecord npcData);

    /// <summary>
    /// Calculate the cumulative experience required to reach a level.
    /// This is the total experience from level 0, not the difference between levels.
    /// </summary>
    int GetCumulativeExperienceForLevel(int level);

    /// <summary>
    /// Check if character has enough experience to level up.
    /// </summary>
    bool CanLevelUp(Character character);

    /// <summary>
    /// Level up a character and return the new level.
    /// </summary>
    int LevelUp(Character character, Ecf classes);
}
