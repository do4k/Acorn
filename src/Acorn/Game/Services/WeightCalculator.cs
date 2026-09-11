using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of weight calculation.
/// </summary>
public class WeightCalculator : IWeightCalculator
{
    public int GetCurrentWeight(Character character, Eif items)
    {
        var totalWeight = 0;
        foreach (var invItem in character.Inventory.Items)
        {
            var itemData = items.GetItem(invItem.Id);
            if (itemData != null)
            {
                totalWeight += itemData.Weight * invItem.Amount;
            }
        }

        return totalWeight;
    }

    public int GetWeight(Eif items, int itemId, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var itemData = items.GetItem(itemId);
        return itemData == null ? 0 : itemData.Weight * amount;
    }

    public bool CanCarry(Character character, Eif items, int itemId, int amount = 1)
    {
        var itemData = items.GetItem(itemId);
        if (itemData == null)
        {
            return false;
        }

        var additionalWeight = itemData.Weight * amount;
        return GetCurrentWeight(character, items) + additionalWeight <= character.MaxWeight;
    }
}