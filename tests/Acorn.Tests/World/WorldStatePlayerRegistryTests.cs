using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acorn.Tests.World;

/// <summary>
///     Player-registry tests for <see cref="WorldState"/> (issue #133): disconnect cleanup is
///     instance-safe, so a late or duplicated cleanup for an old connection can never evict a
///     newer player that was handed the same session id.
/// </summary>
public class WorldStatePlayerRegistryTests
{
    private readonly WorldState _sut = CreateWorldState();

    [Test]
    public void TryRemovePlayer_WhenInstanceMatches_ShouldRemove()
    {
        // Arrange
        var (player, _) = FakePlayer.Create("Resident", 10);
        _sut.TryAddPlayer(10, player).Should().BeTrue();

        // Act
        var removed = _sut.TryRemovePlayer(10, player);

        // Assert
        removed.Should().BeTrue();
        _sut.GetPlayer(10).Should().BeNull();
    }

    [Test]
    public void TryRemovePlayer_WhenInstanceIsDifferent_ShouldKeepCurrentPlayer()
    {
        // Arrange — session id 10 belongs to the resident; a departing old connection claims it too
        var (resident, _) = FakePlayer.Create("Resident", 10);
        _sut.TryAddPlayer(10, resident).Should().BeTrue();
        var (departing, _) = FakePlayer.Create("Departing", 10);

        // Act
        var removed = _sut.TryRemovePlayer(10, departing);

        // Assert — the wrong instance must not evict the right one
        removed.Should().BeFalse();
        _sut.GetPlayer(10).Should().BeSameAs(resident);
    }

    [Test]
    public void IsSessionInUse_ShouldReflectRegistrations()
    {
        var (player, _) = FakePlayer.Create("Resident", 42);
        _sut.IsSessionInUse(42).Should().BeFalse();

        _sut.TryAddPlayer(42, player);
        _sut.IsSessionInUse(42).Should().BeTrue();

        _sut.TryRemovePlayer(42, player);
        _sut.IsSessionInUse(42).Should().BeFalse();
    }

    private static WorldState CreateWorldState()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Maps.Returns(Array.Empty<MapWithId>());
        return new WorldState(dataFiles, null!, NullLogger<WorldState>.Instance);
    }
}
