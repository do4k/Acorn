namespace Acorn.Infrastructure;

/// <summary>
///     Reloads pub data files (ECF/EIF/ENF/ESF) and refreshes the pub cache.
/// </summary>
public interface IPubFileReloadService
{
    /// <summary>
    ///     Re-reads the pub files from disk and refreshes the pub cache.
    /// </summary>
    /// <returns><c>true</c> when the reload succeeded; otherwise <c>false</c>.</returns>
    Task<bool> ReloadAsync();

    /// <summary>
    ///     Refreshes the pub cache from the currently loaded pub data.
    /// </summary>
    Task RefreshCacheAsync();
}
