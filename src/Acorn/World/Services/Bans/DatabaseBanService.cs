using System.Collections.Concurrent;
using Acorn.Database;
using Acorn.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acorn.World.Services.Bans;

/// <summary>
///     Ban store that keeps active bans in memory for fast synchronous lookups and
///     persists changes to the database, so bans survive a restart. Active bans are
///     loaded once at startup.
/// </summary>
public sealed class DatabaseBanService : IBanService
{
    private readonly ConcurrentDictionary<string, BanEntry> _bans = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<DatabaseBanService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseBanService(IServiceScopeFactory scopeFactory, ILogger<DatabaseBanService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        LoadActiveBans();
    }

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
        Persist(key, expiresAt, reason);
    }

    public bool Unban(string key)
    {
        var removed = _bans.TryRemove(key, out _);
        Remove(key);
        return removed;
    }

    public IReadOnlyCollection<string> GetActiveBans()
    {
        var now = DateTime.UtcNow;
        return _bans
            .Where(kvp => kvp.Value.ExpiresAt is null || kvp.Value.ExpiresAt > now)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private void LoadActiveBans()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
            var now = DateTime.UtcNow;

            foreach (var ban in db.Bans.ToList())
            {
                if (ban.ExpiresAt is null || ban.ExpiresAt > now)
                {
                    _bans[ban.Key] = new BanEntry(ban.ExpiresAt, ban.Reason);
                }
                else
                {
                    db.Bans.Remove(ban);
                }
            }

            db.SaveChanges();
            _logger.LogInformation("Loaded {Count} active bans", _bans.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persisted bans");
        }
    }

    private void Persist(string key, DateTime? expiresAt, string? reason)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
            var existing = db.Bans.Find(key);
            if (existing is null)
            {
                db.Bans.Add(new Ban { Key = key, ExpiresAt = expiresAt, Reason = reason });
            }
            else
            {
                existing.ExpiresAt = expiresAt;
                existing.Reason = reason;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist ban {Key}", key);
        }
    }

    private void Remove(string key)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
            var existing = db.Bans.Find(key);
            if (existing is not null)
            {
                db.Bans.Remove(existing);
                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove ban {Key}", key);
        }
    }

    private sealed record BanEntry(DateTime? ExpiresAt, string? Reason);
}
