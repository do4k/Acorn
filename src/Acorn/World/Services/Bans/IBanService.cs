namespace Acorn.World.Services.Bans;

/// <summary>
///     Provides ban lookups and mutations. Implementations are pluggable so the
///     in-memory default can later be replaced with a database-backed store without
///     touching callers.
/// </summary>
public interface IBanService
{
    /// <summary>
    ///     Returns true when the key is currently banned, taking any expiry into account.
    ///     Use <see cref="BanKeys" /> to build keys.
    /// </summary>
    bool IsBanned(string key);

    /// <summary>
    ///     Bans a key. Passing a null <paramref name="expiresAt" /> creates a permanent ban.
    /// </summary>
    void Ban(string key, DateTime? expiresAt = null, string? reason = null);

    /// <summary>
    ///     Removes a ban. Returns true when a ban existed and was removed.
    /// </summary>
    bool Unban(string key);

    /// <summary>
    ///     Returns a snapshot of the currently active ban keys.
    /// </summary>
    IReadOnlyCollection<string> GetActiveBans();
}
