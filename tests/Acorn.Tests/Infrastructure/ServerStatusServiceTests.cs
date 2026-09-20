using Acorn.Infrastructure;
using FluentAssertions;

namespace Acorn.Tests.Infrastructure;

public class ServerStatusServiceTests
{
    [Test]
    public void Uptime_ShouldReflectElapsedTimeSinceConstruction()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = start;
        var sut = new ServerStatusService(() => now);

        sut.StartedAtUtc.Should().Be(start);
        sut.Uptime.Should().Be(TimeSpan.Zero);

        now = start.AddMinutes(90);

        sut.Uptime.Should().Be(TimeSpan.FromMinutes(90));
    }
}
