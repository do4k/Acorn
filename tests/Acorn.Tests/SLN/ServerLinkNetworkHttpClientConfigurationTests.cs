using Acorn.Options;
using Acorn.SLN;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.SLN;

/// <summary>
///     The Refit <see cref="IServerLinkNetworkClient" /> registration configures its
///     HttpClient through <see cref="ServerLinkNetworkHttpClientConfiguration" />.
///     Locks down that the base address, user agent and — per #136 — the explicit
///     per-request timeout are all driven by <see cref="SLNOptions" /> so a hung
///     SLN endpoint cannot occupy the check task for the default 100 seconds.
/// </summary>
public class ServerLinkNetworkHttpClientConfigurationTests
{
    private static IOptions<ServerOptions> CreateServerOptions(int timeoutSeconds)
    {
        return OptionsFactory.Create(new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 0, Y = 0, Map = 0 },
            Hosting = new HostingOptions
            {
                HostName = "localhost",
                Port = 8078,
                WebSocketPort = 8079,
                SLN = new SLNOptions
                {
                    Enabled = true,
                    Url = "https://apollo-games.com/SLN/sln.php",
                    PingRate = 5,
                    UserAgent = "EOSERV-TEST",
                    Zone = "Test",
                    ServerName = "Acorn",
                    Site = "https://game.acornhost.io",
                    TimeoutSeconds = timeoutSeconds
                }
            },
            TickRate = 1000
        });
    }

    private static HttpClient CreateConfiguredClient(int timeoutSeconds)
    {
        var provider = new ServiceCollection()
            .AddSingleton(CreateServerOptions(timeoutSeconds))
            .BuildServiceProvider();
        var client = new HttpClient();

        ServerLinkNetworkHttpClientConfiguration.Configure(provider, client);

        return client;
    }

    [Test]
    public void Configure_ShouldApplyBaseAddressAndUserAgentFromOptions()
    {
        // Act
        using var client = CreateConfiguredClient(10);

        // Assert
        client.BaseAddress.Should().Be(new Uri("https://apollo-games.com/SLN/sln.php"));
        client.DefaultRequestHeaders.TryGetValues("User-Agent", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("EOSERV-TEST");
    }

    [Test]
    public void Configure_ShouldApplyTimeoutFromOptions()
    {
        // Act
        using var client = CreateConfiguredClient(7);

        // Assert
        client.Timeout.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Test]
    public void Configure_WhenOptionsUseDefaultTimeout_ShouldApplyTenSeconds()
    {
        // Arrange: SLNOptions.TimeoutSeconds defaults to 10 (#136).
        // Act
        using var client = CreateConfiguredClient(CreateServerOptions(10).Value.Hosting.SLN.TimeoutSeconds);

        // Assert
        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}
