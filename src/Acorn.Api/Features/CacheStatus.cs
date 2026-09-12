using Acorn.Shared.Caching;

namespace Acorn.Api.Features;

public record CacheStatus
{
    public required string Type { get; init; }
    public bool Healthy { get; init; }
}
