namespace Acorn.Database.Models;

/// <summary>
///     A persisted ban. The key is the prefixed ban-store key produced by
///     <c>BanKeys</c> (for example <c>user:name</c> or <c>hdid:value</c>).
/// </summary>
public class Ban
{
    public required string Key { get; set; }

    /// <summary>When the ban expires; <c>null</c> means permanent.</summary>
    public DateTime? ExpiresAt { get; set; }

    public string? Reason { get; set; }
}
