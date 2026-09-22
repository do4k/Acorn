using Acorn.World;
using FluentAssertions;

namespace Acorn.Tests.World;

/// <summary>
///     Tests for world-tick span sampling (#137): the tick loop must record its metric on
///     every tick but open a span only once per the configured interval, so the dashboard's
///     trace buffer keeps room for packet traces.
/// </summary>
public class WorldTickSpanSamplingTests
{
    [Test]
    public void ShouldEmitTickSpan_OnSampleBoundary_ShouldEmit()
    {
        WorldHostedService.ShouldEmitTickSpan(60, 60).Should().BeTrue();
        WorldHostedService.ShouldEmitTickSpan(120, 60).Should().BeTrue();
    }

    [Test]
    public void ShouldEmitTickSpan_BetweenSamples_ShouldSkip()
    {
        WorldHostedService.ShouldEmitTickSpan(1, 60).Should().BeFalse();
        WorldHostedService.ShouldEmitTickSpan(59, 60).Should().BeFalse();
        WorldHostedService.ShouldEmitTickSpan(61, 60).Should().BeFalse();
    }

    [Test]
    public void ShouldEmitTickSpan_WithIntervalOfOne_ShouldSpanEveryTick()
    {
        Enumerable.Range(1, 5).Should().OnlyContain(tick =>
            WorldHostedService.ShouldEmitTickSpan(tick, 1));
    }

    [Test]
    public void ShouldEmitTickSpan_WithNonPositiveInterval_ShouldBehaveLikeOne()
    {
        WorldHostedService.ShouldEmitTickSpan(7, 0).Should().BeTrue();
        WorldHostedService.ShouldEmitTickSpan(7, -5).Should().BeTrue();
    }

    [Test]
    public void ShouldEmitTickSpan_ShouldMatchDefaultOptionValueOfEveryMinute()
    {
        // One span per 60 ticks at the shipped TickRate of 1s ≈ one per minute.
        Acorn.Tests.TestSupport.FakePlayer.CreateOptions()
            .WorldTickSpanSampleEvery.Should().Be(60);
    }
}
