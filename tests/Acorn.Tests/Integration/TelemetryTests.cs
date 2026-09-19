using System.Collections.Concurrent;
using System.Diagnostics;
using Acorn.Infrastructure.Telemetry;
using FluentAssertions;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Verifies that the server actually emits OpenTelemetry activities for the
///     instrumented hot paths. The test host does not register the OpenTelemetry
///     SDK, so an explicit <see cref="ActivityListener" /> observes the source.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
public class TelemetryTests
{
    private readonly TestServerFixture _fixture;

    public TelemetryTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task PacketHandling_ShouldEmitActivity()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AcornActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();

        activities.Should().Contain(
            activity => (string?)activity.GetTagItem("eo.packet.family") == "Init",
            "the server should emit a span for every handled packet");

        // The world tick loop runs on a timer; give it a moment to produce a span.
        await WaitUntilAsync(() => activities.Any(a => a.DisplayName == "world.tick"),
            TimeSpan.FromSeconds(5));
        activities.Should().Contain(activity => activity.DisplayName == "world.tick",
            "the world tick loop should be instrumented");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
