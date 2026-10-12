using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn;

/// <summary>
///     Service for combat and stat formula calculations.
/// </summary>
public interface IFormulaService
{
    /// <summary>
    ///     Calculate final damage after armor reduction.
    /// </summary>
    int CalculateDamage(int rawDamage, int targetArmor, bool critical = false);

    /// <summary>
    ///     Calculate damage dealt to an NPC.
    ///     <paramref name="currentHp"/> is the NPC's current hit points (its max is <paramref name="npcData"/>.Hp).
    ///     Optional bonus min/max damage is added before rolling (e.g. from a spell's own damage range).
    ///     Critical hits occur when attacking from behind/side, or on the first hit when
    ///     <paramref name="criticalFirstHit"/> is enabled (eoserv's CriticalFirstHit).
    /// </summary>
    int CalculateDamageToNpc(Character character, EnfRecord npcData, int currentHp, bool attackingBackOrSide = false,
        int bonusMinDamage = 0, int bonusMaxDamage = 0, bool criticalFirstHit = false);

    /// <summary>
    ///     Calculate damage dealt by an NPC to a player.
    /// </summary>
    int CalculateNpcDamageToPlayer(EnfRecord npcData, Character target, bool attackingBackOrSide = false);

    /// <summary>
    ///     Calculate damage dealt to a player.
    ///     Optional bonus min/max damage is added before rolling (e.g. from a spell's own damage range).
    ///     Critical hits occur when attacking from behind/side, or on the first hit when
    ///     <paramref name="criticalFirstHit"/> is enabled (eoserv's CriticalFirstHit).
    /// </summary>
    int CalculateDamageToPlayer(Character attacker, Character target, bool attackingBackOrSide = false,
        int bonusMinDamage = 0, int bonusMaxDamage = 0, bool criticalFirstHit = false);

    /// <summary>
    ///     Calculate the cumulative experience required to reach a level.
    ///     This is the total experience from level 0, not the difference between levels.
    /// </summary>
    int GetCumulativeExperienceForLevel(int level);

    /// <summary>
    ///     Check if character has enough experience to level up.
    /// </summary>
    bool CanLevelUp(Character character);

    /// <summary>
    ///     Level up a character and return the new level.
    /// </summary>
    int LevelUp(Character character, Ecf classes);
}
