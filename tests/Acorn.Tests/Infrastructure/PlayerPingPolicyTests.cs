using Acorn.Infrastructure;
using Acorn.Net.Models;
using FluentAssertions;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     Covers the ping/handshake/login policy used by <see cref="PlayerPingHostedService" />.
/// </summary>
public class PlayerPingPolicyTests
{
    private static PlayerPingHostedService.PingDecision Decide(ClientState state, double ageSeconds,
        int hangupDelaySeconds = 10, int loginTimeoutSeconds = 120)
    {
        return PlayerPingHostedService.Decide(state, TimeSpan.FromSeconds(ageSeconds),
            hangupDelaySeconds, loginTimeoutSeconds);
    }

    [Test]
    public void Decide_WhenHandshakeIncompletePastHangup_ShouldDisconnectForHandshake()
    {
        Decide(ClientState.Initialized, ageSeconds: 11).Should()
            .Be(PlayerPingHostedService.PingDecision.DisconnectHandshakeTimeout);
    }

    [Test]
    public void Decide_WhenHandshakeIncompleteWithinHangup_ShouldSkip()
    {
        Decide(ClientState.Initialized, ageSeconds: 5).Should()
            .Be(PlayerPingHostedService.PingDecision.Skip);
    }

    [Test]
    public void Decide_WhenAcceptedPastLoginTimeout_ShouldDisconnectForLogin()
    {
        Decide(ClientState.Accepted, ageSeconds: 121).Should()
            .Be(PlayerPingHostedService.PingDecision.DisconnectLoginTimeout);
    }

    [Test]
    public void Decide_WhenAcceptedWithinLoginTimeout_ShouldNotPing()
    {
        Decide(ClientState.Accepted, ageSeconds: 30).Should()
            .Be(PlayerPingHostedService.PingDecision.Skip);
    }

    [Test]
    public void Decide_WhenLoggedIn_ShouldPing()
    {
        Decide(ClientState.LoggedIn, ageSeconds: 3600).Should()
            .Be(PlayerPingHostedService.PingDecision.Ping);
    }

    [Test]
    public void Decide_WhenInGame_ShouldPing()
    {
        Decide(ClientState.InGame, ageSeconds: 3600).Should()
            .Be(PlayerPingHostedService.PingDecision.Ping);
    }

    [Test]
    public void Decide_WhenLoginTimeoutDisabled_ShouldNotDisconnectAcceptedConnection()
    {
        Decide(ClientState.Accepted, ageSeconds: 100_000, loginTimeoutSeconds: 0).Should()
            .Be(PlayerPingHostedService.PingDecision.Skip);
    }

    [Test]
    public void Decide_WhenAllTimeoutsDisabled_ShouldSkipEverything()
    {
        Decide(ClientState.Uninitialized, ageSeconds: 100_000, hangupDelaySeconds: 0, loginTimeoutSeconds: 0)
            .Should().Be(PlayerPingHostedService.PingDecision.Skip);
    }
}
