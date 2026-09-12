namespace Acorn.Tests.Integration;

/// <summary>
///     Shared fixture key for the auth and login integration test classes. Sharing one
///     server fixture avoids spinning up an extra server (and the ephemeral-port races
///     that come with it), while the matching keyed <c>NotInParallel</c> attribute keeps
///     their tests running sequentially against the same host.
/// </summary>
internal static class IntegrationServerKey
{
    public const string Name = "IntegrationServer";
}
