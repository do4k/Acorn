namespace Acorn.Shared.Models.Online;

/// <summary>
/// Brief summary of an online player.
/// </summary>
public record OnlinePlayerSummary
{
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string Class { get; init; } = string.Empty;
    public int MapId { get; init; }
}
