using Acorn.Shared.Models.Online;

namespace Acorn.Shared.Caching;

/// <summary>
/// Service for caching and retrieving online character data from Redis.
/// </summary>
public interface ICharacterCacheService
{
    /// <summary>
    /// Cache an online character's state.
    /// </summary>
    Task CacheCharacterAsync(OnlineCharacterRecord character);

    /// <summary>
    /// Get an online character by name.
    /// </summary>
    Task<OnlineCharacterRecord?> GetCharacterByNameAsync(string name);

    /// <summary>
    /// Get an online character by session ID.
    /// </summary>
    Task<OnlineCharacterRecord?> GetCharacterBySessionIdAsync(int sessionId);

    /// <summary>
    /// Get all online characters.
    /// </summary>
    Task<IReadOnlyList<OnlineCharacterRecord>> GetAllOnlineCharactersAsync();

    /// <summary>
    /// Get online player summary.
    /// </summary>
    Task<OnlinePlayersRecord> GetOnlinePlayersAsync();

    /// <summary>
    /// Remove a character from online cache.
    /// </summary>
    Task RemoveCharacterAsync(string name);

    /// <summary>
    /// Remove a character from online cache by session ID.
    /// </summary>
    Task RemoveCharacterBySessionIdAsync(int sessionId);
}
