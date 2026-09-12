using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
///     A single item (and amount) offered in a trade.
/// </summary>
public record TradeOffer(int ItemId, int Amount);
