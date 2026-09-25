using System.Collections.Concurrent;
using Acorn.Extensions;
using Acorn.Options;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure.Security;

public class AccountLockoutService(IOptions<ServerOptions> options, UtcNowDelegate now) : IAccountLockoutService
{
    private readonly record struct Attempt(int Failures, DateTime WindowStart, DateTime? LockedUntil);

    private readonly ConcurrentDictionary<string, Attempt> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxAttempts = options.Value.AccountLockoutAttempts;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(options.Value.AccountLockoutMinutes);
    private readonly TimeSpan _lockout = TimeSpan.FromMinutes(options.Value.AccountLockoutMinutes);
    private readonly UtcNowDelegate _now = now;

    public bool IsLockedOut(string username)
    {
        if (_maxAttempts <= 0 || _window <= TimeSpan.Zero || _lockout <= TimeSpan.Zero)
        {
            return false;
        }

        var now = _now();
        if (!_attempts.TryGetValue(username, out var attempt))
        {
            return false;
        }

        if (attempt.LockedUntil is { } lockedUntil)
        {
            if (lockedUntil > now)
            {
                return true;
            }

            _attempts.TryRemove(username, out _);
            return false;
        }

        if (now - attempt.WindowStart > _window)
        {
            _attempts.TryRemove(username, out _);
        }

        return false;
    }

    public void RecordFailure(string username)
    {
        if (_maxAttempts <= 0 || _window <= TimeSpan.Zero || _lockout <= TimeSpan.Zero)
        {
            return;
        }

        var now = _now();
        _attempts.AddOrUpdate(username,
            _ => new Attempt(1, now, 1 >= _maxAttempts ? now + _lockout : null),
            (_, attempt) =>
            {
                if (now - attempt.WindowStart > _window)
                {
                    return new Attempt(1, now, null);
                }

                if (attempt.LockedUntil is not null)
                {
                    return attempt;
                }

                var failures = attempt.Failures + 1;
                return failures >= _maxAttempts
                    ? new Attempt(failures, attempt.WindowStart, now + _lockout)
                    : new Attempt(failures, attempt.WindowStart, null);
            });

        // Bound memory: spraying random usernames must not grow the table without limit.
        if (_attempts.Count > 10000)
        {
            PruneExpired(now);
        }
    }

    public void RecordSuccess(string username)
    {
        _attempts.TryRemove(username, out _);
    }

    private void PruneExpired(DateTime now)
    {
        foreach (var (username, attempt) in _attempts)
        {
            var expired = attempt.LockedUntil is { } lockedUntil
                ? lockedUntil <= now
                : now - attempt.WindowStart > _window;

            if (expired)
            {
                _attempts.TryRemove(username, out _);
            }
        }
    }
}
