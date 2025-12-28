namespace Acorn.Shared.Models;

/// <summary>
/// Error response with additional details.
/// </summary>
public record ApiErrorDetails : ApiError
{
    public object? Details { get; init; }
}

