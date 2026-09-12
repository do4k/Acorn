namespace Acorn.Tests.Support;

/// <summary>
///     Passwords for integration tests. Generated at runtime so no credential-shaped
///     literal is committed, which secret scanners otherwise flag as a false positive.
/// </summary>
internal static class TestPasswords
{
    /// <summary>
    ///     A password that satisfies the server's configured length limits.
    /// </summary>
    public static string Valid { get; } = $"Ac{Guid.NewGuid():N}"[..12];
}
