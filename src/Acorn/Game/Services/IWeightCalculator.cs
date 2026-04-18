using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Service responsible for calculating character weight and carry capacity.
/// </summary>
public interface IWeightCalculator
{
    /// <summary>
    ///     Calculates the current weight of items in inventory.
    /// </summary>
    int GetCurrentWeight(Character character, Eif items);

    /// <summary>
    ///     Checks if adding an item would exceed weight limit.
    /// </summary>
    bool CanCarry(Character character, Eif items, int itemId, int amount = 1);
}