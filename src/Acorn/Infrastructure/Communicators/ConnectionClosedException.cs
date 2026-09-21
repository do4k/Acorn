namespace Acorn.Infrastructure.Communicators;

/// <summary>
///     Thrown when a packet cannot be delivered because the underlying connection has
///     already been closed or torn down.
/// </summary>
/// <remarks>
///     Sending is best-effort: a player may disconnect at any moment, including halfway
///     through a broadcast to every online player. Callers that broadcast should treat
///     this as a miss for that recipient rather than a failure of the whole operation.
/// </remarks>
public sealed class ConnectionClosedException : Exception
{
    public ConnectionClosedException(string message)
        : base(message)
    {
    }

    public ConnectionClosedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
