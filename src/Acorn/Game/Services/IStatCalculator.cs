using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
/// Service responsible for calculating character stats based on level, class, and equipment.
/// </summary>
public interface IStatCalculator
{
    /// <summary>
    /// Recalculates all derived stats (MaxHp, MaxTp, MaxSp, damage, armor, etc.) for a character.
    /// </summary>
    void RecalculateStats(Character character, Ecf classes);
}
