namespace Acorn.Shared.Models;

/// <summary>
/// Error response for not found scenarios.
/// </summary>
public record NotFoundError : ApiError
{
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
}

