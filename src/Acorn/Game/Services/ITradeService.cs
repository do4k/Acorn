using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
///     A single item (and amount) offered in a trade.
/// </summary>
public record TradeOffer(int ItemId, int Amount);

/// <summary>
///     Outcome of attempting to complete a trade.
/// </summary>
public enum TradeCompletionStatus
{
    Success,
    PlayerMissingItems,
    PartnerMissingItems,
    PlayerOverweight,
    PartnerOverweight
}

/// <summary>
///     Result of a trade completion attempt.
/// </summary>
public record TradeCompletionResult(TradeCompletionStatus Status)
{
    public bool Success => Status == TradeCompletionStatus.Success;
}

/// <summary>
///     Service responsible for validating and atomically executing a trade between two characters.
/// </summary>
public interface ITradeService
{
    /// <summary>
    ///     Validates that both sides still hold their offered items and can carry what they
    ///     receive, then performs the swap. Nothing is mutated when validation fails.
    /// </summary>
    TradeCompletionResult TryCompleteTrade(
        Character player,
        IReadOnlyCollection<TradeOffer> playerItems,
        Character partner,
        IReadOnlyCollection<TradeOffer> partnerItems);
}
