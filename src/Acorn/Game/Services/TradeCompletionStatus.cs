using Acorn.Game.Models;

namespace Acorn.Game.Services;

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
