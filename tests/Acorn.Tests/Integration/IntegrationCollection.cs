using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Shares a single <see cref="TestServerFixture" /> across the auth and login
///     integration test classes. Keeping them in one collection avoids spinning up
///     an extra server (and the ephemeral-port races that come with it) while still
///     running the tests sequentially against the same host.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<TestServerFixture>
{
    public const string Name = "IntegrationServer";
}
