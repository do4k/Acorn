using Acorn.Game.Models;

namespace Acorn.Game.Services;

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
