using Acorn.Extensions;

namespace Acorn.Infrastructure;

/// <summary>
///     Tracks when the server started so uptime can be reported.
/// </summary>
public class ServerStatusService : IServerStatusService
{
    private readonly UtcNowDelegate _utcNow;

    public ServerStatusService(UtcNowDelegate utcNow)
    {
        _utcNow = utcNow;
        StartedAtUtc = utcNow();
    }

    public DateTime StartedAtUtc { get; }

    public TimeSpan Uptime => _utcNow() - StartedAtUtc;
}
