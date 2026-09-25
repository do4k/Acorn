using Acorn.Extensions;
using Acorn.Infrastructure.Security;
using Acorn.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acorn.Tests.Infrastructure.Security;

public class AccountLockoutServiceTests
{
    private DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ServerOptions CreateOptions(int attempts, int minutes)
    {
        return new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 1, Y = 1, Map = 1 },
            Hosting = new HostingOptions
            {
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "",
                    PingRate = 1,
                    UserAgent = "",
                    Zone = "",
                    ServerName = "",
                    Site = ""
                },
                HostName = "localhost",
                Port = 1,
                WebSocketPort = 2
            },
            TickRate = 100,
            AccountLockoutAttempts = attempts,
            AccountLockoutMinutes = minutes
        };
    }

    private AccountLockoutService CreateService(int attempts = 3, int minutes = 15)
    {
        UtcNowDelegate clock = () => _now;
        return new AccountLockoutService(Microsoft.Extensions.Options.Options.Create(CreateOptions(attempts, minutes)), clock);
    }

    [Test]
    public void IsLockedOut_AfterThresholdFailures_ShouldBeTrue()
    {
        var sut = CreateService(attempts: 3, minutes: 15);

        sut.RecordFailure("victim");
        sut.RecordFailure("victim");
        sut.IsLockedOut("victim").Should().BeFalse();

        sut.RecordFailure("victim");
        sut.IsLockedOut("victim").Should().BeTrue();
    }

    [Test]
    public void IsLockedOut_AfterLockoutExpires_ShouldBeFalse()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        UtcNowDelegate clock = () => now;
        var sut = new AccountLockoutService(Microsoft.Extensions.Options.Options.Create(CreateOptions(1, 15)), clock);

        sut.RecordFailure("victim");
        sut.IsLockedOut("victim").Should().BeTrue();

        now = now.AddMinutes(16);
        sut.IsLockedOut("victim").Should().BeFalse();
    }

    [Test]
    public void RecordSuccess_ShouldClearFailures()
    {
        var sut = CreateService(attempts: 2, minutes: 15);

        sut.RecordFailure("victim");
        sut.RecordSuccess("victim");
        sut.RecordFailure("victim");

        sut.IsLockedOut("victim").Should().BeFalse();
    }

    [Test]
    public void IsLockedOut_WhenDisabled_ShouldNeverLock()
    {
        var sut = CreateService(attempts: 0, minutes: 0);

        for (var i = 0; i < 100; i++)
        {
            sut.RecordFailure("victim");
        }

        sut.IsLockedOut("victim").Should().BeFalse();
    }

    [Test]
    public void IsLockedOut_OtherUsernames_ShouldBeUnaffected()
    {
        var sut = CreateService(attempts: 1, minutes: 15);

        sut.RecordFailure("victim");

        sut.IsLockedOut("other").Should().BeFalse();
    }
}
