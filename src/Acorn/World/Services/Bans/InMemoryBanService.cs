using System.Collections.Concurrent;

namespace Acorn.World.Services.Bans;

/// <summary>
///     Thread-safe, process-local ban store. Bans are lost on restart; a persistent
///     store can be swapped in later via <see cref="IBanService" />.
/// </summary>
public sealed class InMemoryBanService : IBanService
{
    private readonly ConcurrentDictionary<string, BanEntry> _bans = new(StringComparer.OrdinalIgnoreCase);

    public bool IsBanned(string key)
    {
        if (!_bans.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            _bans.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    public void Ban(string key, DateTime? expiresAt = null, string? reason = null)
    {
        _bans[key] = new BanEntry(expiresAt, reason);
    }

    public bool Unban(string key)
    {
        return _bans.TryRemove(key, out _);
    }

    public IReadOnlyCollection<string> GetActiveBans()
    {
        var now = DateTime.UtcNow;
        return _bans
            .Where(kvp => kvp.Value.ExpiresAt is null || kvp.Value.ExpiresAt > now)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private sealed record BanEntry(DateTime? ExpiresAt, string? Reason);
}
