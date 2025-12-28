namespace Acorn.Shared.Models;

/// <summary>
/// Standard error response for API endpoints.
/// </summary>
public record ApiError
{
    public required string Error { get; init; }
    public string? Message { get; init; }
}

