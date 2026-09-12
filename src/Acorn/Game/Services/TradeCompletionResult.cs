using Acorn.Game.Models;

namespace Acorn.Game.Services;

/// <summary>
///     Result of a trade completion attempt.
/// </summary>
public record TradeCompletionResult(TradeCompletionStatus Status)
{
    public bool Success => Status == TradeCompletionStatus.Success;
}
