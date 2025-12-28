namespace Acorn.Shared.Models.Online;

/// <summary>
/// Summary of online players.
/// </summary>
public record OnlinePlayersRecord
{
    public int TotalOnline { get; init; }
    public IReadOnlyList<OnlinePlayerSummary> Players { get; init; } = [];
}

