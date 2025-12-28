namespace Acorn.Shared.Models;

/// <summary>
/// Error response for service unavailable scenarios.
/// </summary>
public record ServiceUnavailableError : ApiError
{
    public string? Service { get; init; }
}
