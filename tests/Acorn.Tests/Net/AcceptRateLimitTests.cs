using System.Net;
using Acorn.Net;
using Acorn.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acorn.Tests.Net;

public class AcceptRateLimitTests
{
    [Test]
    public void ShouldAccept_MoreConnectionsThanConfigured_ShouldRejectExcess()
    {
        var time = new FakeTimeProvider();
        var limiter = new AcceptRateLimiter(CreateOptions(maxAccepts: 3, windowSeconds: 10), time);
        var ip = IPAddress.Parse("203.0.113.10");

        for (var i = 0; i < 3; i++)
        {
            limiter.ShouldAccept(ip).Should().BeTrue($"connection {i + 1} of 3 is within the limit");
        }

        limiter.ShouldAccept(ip).Should().BeFalse("the 4th connection exceeds 3 accepts per window");
    }

    [Test]
    public void ShouldAccept_AfterWindowExpires_ShouldAllowAgain()
    {
        var time = new FakeTimeProvider();
        var limiter = new AcceptRateLimiter(CreateOptions(maxAccepts: 2, windowSeconds: 10), time);
        var ip = IPAddress.Parse("203.0.113.20");

        limiter.ShouldAccept(ip).Should().BeTrue();
        limiter.ShouldAccept(ip).Should().BeTrue();
        limiter.ShouldAccept(ip).Should().BeFalse("2 accepts already used in this window");

        time.Advance(TimeSpan.FromSeconds(11));

        limiter.ShouldAccept(ip).Should().BeTrue("the window expired, so the counter reset");
    }

    [Test]
    public void ShouldAccept_LoopbackOrPrivateSource_ShouldNeverLimit()
    {
        var time = new FakeTimeProvider();
        var limiter = new AcceptRateLimiter(CreateOptions(maxAccepts: 2, windowSeconds: 10), time);
        IPAddress[] localAddresses =
        [
            IPAddress.Loopback,
            IPAddress.IPv6Loopback,
            IPAddress.Parse("10.0.0.7"),
            IPAddress.Parse("172.16.4.2"),
            IPAddress.Parse("192.168.1.50"),
            IPAddress.Parse("169.254.10.10"),
            IPAddress.Parse("100.64.1.1"),
            IPAddress.Parse("fd00::1"),
        ];

        foreach (var address in localAddresses)
        {
            for (var i = 0; i < 50; i++)
            {
                limiter.ShouldAccept(address).Should().BeTrue(
                    $"{address} must bypass the limiter so proxied or LAN clients never share a bucket");
            }
        }
    }

    [Test]
    public void ShouldAccept_DifferentPublicIps_ShouldTrackSeparately()
    {
        var time = new FakeTimeProvider();
        var limiter = new AcceptRateLimiter(CreateOptions(maxAccepts: 1, windowSeconds: 10), time);
        var first = IPAddress.Parse("203.0.113.1");
        var second = IPAddress.Parse("198.51.100.2");

        limiter.ShouldAccept(first).Should().BeTrue();
        limiter.ShouldAccept(first).Should().BeFalse("the first IP is over its own limit");

        limiter.ShouldAccept(second).Should().BeTrue("a different IP has its own budget");
    }

    [Test]
    public void ShouldAccept_LimitDisabled_ShouldAlwaysAllow()
    {
        var time = new FakeTimeProvider();
        var limiter = new AcceptRateLimiter(CreateOptions(maxAccepts: 0, windowSeconds: 10), time);
        var ip = IPAddress.Parse("203.0.113.30");

        for (var i = 0; i < 100; i++)
        {
            limiter.ShouldAccept(ip).Should().BeTrue("MaxAcceptsPerIp = 0 disables the limit");
        }
    }

    private static IOptions<ServerOptions> CreateOptions(int maxAccepts, int windowSeconds) =>
        Microsoft.Extensions.Options.Options.Create(new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 1, Y = 1, Map = 1 },
            Hosting = new HostingOptions
            {
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "http://localhost",
                    PingRate = 5,
                    UserAgent = "Test",
                    Zone = "Test",
                    ServerName = "Test",
                    Site = "http://localhost",
                },
                HostName = "localhost",
                Port = 1234,
                WebSocketPort = 1235,
            },
            TickRate = 1000,
            MaxAcceptsPerIp = maxAccepts,
            AcceptWindowSeconds = windowSeconds,
        });

    /// <summary>Deterministic clock so window expiry is tested without sleeping.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
