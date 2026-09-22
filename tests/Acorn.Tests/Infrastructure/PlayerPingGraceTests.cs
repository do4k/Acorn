using Acorn.Infrastructure;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     Tests for the missed-pong grace interval (issue #135): one unanswered ping is a
///     network hiccup, not a dead player. Only after MaxMissedPings consecutive unanswered
///     pings may the sweep disconnect, and any pong must reset the counter.
/// </summary>
public class PlayerPingGraceTests
{
    [Test]
    public void ExceededPingGrace_WhileWithinGrace_ShouldAllowAnotherProbe()
    {
        PlayerPingHostedService.ExceededPingGrace(missedPings: 1, maxMissedPings: 2)
            .Should().BeFalse();
    }

    [Test]
    public void ExceededPingGrace_WhenGraceExhausted_ShouldDrop()
    {
        PlayerPingHostedService.ExceededPingGrace(missedPings: 2, maxMissedPings: 2)
            .Should().BeTrue();
    }

    [Test]
    public void ExceededPingGrace_WhenLimitIsOne_ShouldBehaveLikeLegacySingleMiss()
    {
        PlayerPingHostedService.ExceededPingGrace(missedPings: 1, maxMissedPings: 1)
            .Should().BeTrue();
    }

    [Test]
    public void ServerOptions_Default_ShouldAllowTwoConsecutiveMisses()
    {
        FakePlayer.CreateOptions().MaxMissedPings.Should().Be(2);
    }

    [Test]
    public async Task HandleAsync_WhenPongArrives_ShouldResetNeedPongAndMissedCounter()
    {
        // Arrange — the sweep has two unanswered pings on the books (still within grace)
        var (player, _) = FakePlayer.Create("Tester", 5);
        player.NeedPong = true;
        player.MissedPings = 2;

        var sut = new ConnectionPingClientPacketHandler();

        // Act
        await sut.HandleAsync(player, new ConnectionPingClientPacket());

        // Assert — the reply cancels the whole streak, not just one probe
        player.NeedPong.Should().BeFalse();
        player.MissedPings.Should().Be(0);
    }
}
