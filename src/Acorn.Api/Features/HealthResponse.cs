using Acorn.Shared.Caching;

namespace Acorn.Api.Features;

public record HealthResponse
{
    public required string Status { get; init; }
    public required CacheStatus Cache { get; init; }
    public DateTime Timestamp { get; init; }
}
