namespace Acorn.Infrastructure;

/// <summary>
///     Provides server process lifetime information.
/// </summary>
public interface IServerStatusService
{
    /// <summary>
    ///     When the current server process started, in UTC.
    /// </summary>
    DateTime StartedAtUtc { get; }

    /// <summary>
    ///     How long the server has been running.
    /// </summary>
    TimeSpan Uptime { get; }
}
