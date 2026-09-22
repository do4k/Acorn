using Acorn.Database.Repository;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Net;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     Regression tests for the ping sweep's reap path (issue #134): timed-out sessions must be
///     dropped from the world state once and warned about once, never looping "disconnecting"
///     warnings every interval for connections whose transport is already gone.
/// </summary>
public class PlayerPingReapTests
{
    private const int HangupSeconds = 10;

    [Test]
    public async Task PingAllPlayers_WhenHandshakeTimedOutAndTransportDead_ShouldReapWorldEntry()
    {
        // Arrange — a ghost session: pre-accept, past the hangup delay, transport already gone
        var world = CreateWorldState();
        var (ghost, _) = FakePlayer.Create("Ghost", 10);
        ghost.ConnectedAt = DateTime.UtcNow.AddSeconds(-(HangupSeconds + 1));
        world.TryAddPlayer(10, ghost).Should().BeTrue();

        var sut = CreateService(world);

        // Act
        await sut.PingAllPlayersAsync();

        // Assert
        ghost.ReapWarningLogged.Should().BeTrue("the first reap logs once");
        world.Players.Should().BeEmpty("dead timed-out sessions must not linger in the world");
    }

    [Test]
    public async Task PingAllPlayers_WhenAlreadyWarnedSessionIsSweptAgain_ShouldNotWarnAgainButStillReap()
    {
        // Arrange — flag set by a previous sweep; world entry restored to prove the
        // follow-up sweep still finishes the job instead of only logging
        var world = CreateWorldState();
        var (ghost, _) = FakePlayer.Create("Ghost", 10);
        ghost.ConnectedAt = DateTime.UtcNow.AddSeconds(-(HangupSeconds + 1));
        ghost.ReapWarningLogged = true;
        world.TryAddPlayer(10, ghost).Should().BeTrue();

        var sut = CreateService(world);

        // Act
        await sut.PingAllPlayersAsync();

        // Assert
        world.Players.Should().BeEmpty();
    }

    [Test]
    public async Task PingAllPlayers_WhenTimedOutConnectionStillLive_ShouldNotReap()
    {
        // Arrange — same timeout, but the transport is still connected: the sweep may only
        // cancel the token; the read loop owns the removal and disposal.
        var world = CreateWorldState();
        var liveCommunicator = Substitute.For<ICommunicator>();
        liveCommunicator.IsConnected.Returns(true);
        var playerState = FakePlayer.Create("Live", 11, liveCommunicator);
        playerState.ConnectedAt = DateTime.UtcNow.AddSeconds(-(HangupSeconds + 1));
        world.TryAddPlayer(11, playerState).Should().BeTrue();

        var sut = CreateService(world);

        // Act
        await sut.PingAllPlayersAsync();

        // Assert — the sweep must not yank a live-transport session out of the world,
        // nor dispose its transport (that belongs to the read loop's cleanup)
        world.Players.Should().ContainKey(11);
        await liveCommunicator.DidNotReceive().CloseAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PingAllPlayers_WhenReaping_ShouldDisposeTransportExactlyOnce()
    {
        // Arrange
        var world = CreateWorldState();
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);
        var ghost = FakePlayer.Create("Ghost", 12, communicator);
        ghost.ConnectedAt = DateTime.UtcNow.AddSeconds(-(HangupSeconds + 1));
        world.TryAddPlayer(12, ghost).Should().BeTrue();

        var sut = CreateService(world);

        // Act — two sweeps; the session is reaped by the first
        await sut.PingAllPlayersAsync();
        await sut.PingAllPlayersAsync();

        // Assert — close ran once, and Dispose is idempotent even if called again
        await communicator.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        ghost.Dispose();
        ghost.Dispose();
        await communicator.Received(1).CloseAsync(Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static PlayerPingHostedService CreateService(WorldState world)
    {
        var options = FakePlayer.CreateOptions();
        options.HangupDelaySeconds = HangupSeconds;
        return new PlayerPingHostedService(
            NullLogger<PlayerPingHostedService>.Instance,
            world,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private static WorldState CreateWorldState()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Maps.Returns(Array.Empty<MapWithId>());
        return new WorldState(dataFiles, null!, NullLogger<WorldState>.Instance);
    }
}
