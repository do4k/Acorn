using Acorn.Database.Repository;
using Acorn.Game.Models;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Game.Services;

/// <summary>
///     Default implementation of trade validation and execution.
/// </summary>
public class TradeService(
    IInventoryService inventoryService,
    IWeightCalculator weightCalculator,
    IDataFileRepository dataFileRepository) : ITradeService
{
    public TradeCompletionResult TryCompleteTrade(
        Character player,
        IReadOnlyCollection<TradeOffer> playerItems,
        Character partner,
        IReadOnlyCollection<TradeOffer> partnerItems)
    {
        // Both players must still hold everything they offered. If not, the trade is
        // stale (items were dropped, sold, equipped, etc.) and must be aborted without
        // mutating either inventory.
        if (!HoldsAll(player, playerItems))
        {
            return new TradeCompletionResult(TradeCompletionStatus.PlayerMissingItems);
        }

        if (!HoldsAll(partner, partnerItems))
        {
            return new TradeCompletionResult(TradeCompletionStatus.PartnerMissingItems);
        }

        var eif = dataFileRepository.Eif;

        // Weight is evaluated against the resulting inventory: what each player gives
        // away is removed first, so a straight swap of equally heavy items stays valid.
        if (!CanCarryAfterSwap(player, playerItems, partnerItems, eif))
        {
            return new TradeCompletionResult(TradeCompletionStatus.PlayerOverweight);
        }

        if (!CanCarryAfterSwap(partner, partnerItems, playerItems, eif))
        {
            return new TradeCompletionResult(TradeCompletionStatus.PartnerOverweight);
        }

        // Validation passed. Perform the swap with no awaits so it cannot be interrupted
        // part-way through.
        foreach (var offer in playerItems)
        {
            inventoryService.TryRemoveItem(player, offer.ItemId, offer.Amount);
            inventoryService.TryAddItem(partner, offer.ItemId, offer.Amount);
        }

        foreach (var offer in partnerItems)
        {
            inventoryService.TryRemoveItem(partner, offer.ItemId, offer.Amount);
            inventoryService.TryAddItem(player, offer.ItemId, offer.Amount);
        }

        return new TradeCompletionResult(TradeCompletionStatus.Success);
    }

    private bool HoldsAll(Character character, IReadOnlyCollection<TradeOffer> offers)
    {
        return offers.All(offer =>
            offer.Amount > 0 && inventoryService.HasItem(character, offer.ItemId, offer.Amount));
    }

    private bool CanCarryAfterSwap(
        Character character,
        IReadOnlyCollection<TradeOffer> giving,
        IReadOnlyCollection<TradeOffer> receiving,
        Eif eif)
    {
        var resultingWeight = weightCalculator.GetCurrentWeight(character, eif);

        foreach (var offer in receiving)
        {
            resultingWeight += weightCalculator.GetWeight(eif, offer.ItemId, offer.Amount);
        }

        foreach (var offer in giving)
        {
            resultingWeight -= weightCalculator.GetWeight(eif, offer.ItemId, offer.Amount);
        }

        return resultingWeight <= character.MaxWeight;
    }
}
