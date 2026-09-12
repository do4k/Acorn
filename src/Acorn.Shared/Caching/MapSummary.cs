using Acorn.Shared.Models.Maps;

namespace Acorn.Shared.Caching;

/// <summary>
/// Brief summary of a map.
/// </summary>
public record MapSummary
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PlayerCount { get; init; }
    public int NpcCount { get; init; }
}
