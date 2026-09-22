using Acorn.Options;
using FluentAssertions;

namespace Acorn.Tests.Options;

/// <summary>
///     The SLN check runs against an external endpoint every few minutes. An explicit
///     per-request timeout keeps a hung endpoint from holding the check task for the
///     <see cref="HttpClient" /> default of 100 seconds (#136), and the default must
///     be generous enough for the observed ~1.5 s round-trip.
/// </summary>
public class SLNOptionsTests
{
    [Test]
    public void NewOptions_ShouldDefaultTimeoutSecondsToTen()
    {
        // Arrange
        var sut = new SLNOptions
        {
            Enabled = false,
            Url = "https://apollo-games.com/SLN/sln.php",
            PingRate = 5,
            UserAgent = "EOSERV",
            Zone = "Test",
            ServerName = "Acorn",
            Site = "https://game.acornhost.io"
        };

        // Act / Assert
        sut.TimeoutSeconds.Should().Be(10);
    }

    [Test]
    public void TimeoutSeconds_ShouldBeSettable()
    {
        // Arrange
        var sut = new SLNOptions
        {
            Enabled = false,
            Url = "https://apollo-games.com/SLN/sln.php",
            PingRate = 5,
            UserAgent = "EOSERV",
            Zone = "Test",
            ServerName = "Acorn",
            Site = "https://game.acornhost.io",
            TimeoutSeconds = 30
        };

        // Act / Assert
        sut.TimeoutSeconds.Should().Be(30);
    }
}
