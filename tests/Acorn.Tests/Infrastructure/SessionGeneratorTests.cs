using Acorn.Database.Repository;
using Acorn.Infrastructure;
using Acorn.Net;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using NSubstitute;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     Regression tests for session-ID generation. Session IDs are sent back to the
///     client inside an <see cref="AccountReply"/> slot, so they must never collide
///     with the reserved <see cref="AccountReply"/> enum values — a hard-won fix
///     (see SessionGenerator). Locking this down prevents future desyncs where the
///     client mistakes a session ID for an account-create response code.
/// </summary>
public class SessionGeneratorTests
{
    private const int MinSessionId = 8;
    private const int MaxSessionId = (int)EoNumericLimits.SHORT_MAX - 1;

    private readonly WorldState _worldState = CreateWorldState();

    // AccountReply reserved values that must never be used as a session ID.
    private static readonly int[] ReservedReplyCodes =
        Enum.GetValues<AccountReply>().Select(v => (int)v).ToArray();

    [Test]
    public void Generate_WhenCalledManyTimes_ShouldNeverCollideWithAccountReplyReservedValues()
    {
        // Arrange
        var sut = new SessionGenerator(_worldState);
        var reservedMin = ReservedReplyCodes.Min();
        var reservedMax = ReservedReplyCodes.Max();

        // Act
        var ids = Enumerable.Range(0, 10_000).Select(_ => sut.Generate()).ToList();

        // Assert — none may fall inside the reserved AccountReply range [1..7]
        ids.Should().OnlyContain(id => id < reservedMin || id > reservedMax);
    }

    [Test]
    public void Generate_WhenCalledManyTimes_ShouldStayWithinSessionIdRange()
    {
        var sut = new SessionGenerator(_worldState);

        var ids = Enumerable.Range(0, 10_000).Select(_ => sut.Generate()).ToList();

        ids.Should().OnlyContain(id => id >= MinSessionId);
        ids.Should().OnlyContain(id => id < (int)EoNumericLimits.SHORT_MAX);
    }

    [Test]
    public void Generate_ShouldNeverEqualAnyAccountReplyEnumValue()
    {
        var sut = new SessionGenerator(_worldState);
        for (var i = 0; i < 10_000; i++)
        {
            var id = sut.Generate();
            ReservedReplyCodes.Should().NotContain(id);
        }
    }

    [Test]
    public void Generate_ShouldIssueConsecutiveIds()
    {
        // Arrange
        var sut = new SessionGenerator(_worldState);

        // Act
        var ids = Enumerable.Range(0, 4).Select(_ => sut.Generate()).ToList();

        // Assert — a monotonic walk, so reuse of an id takes a full cycle
        ids.Should().Equal(MinSessionId, MinSessionId + 1, MinSessionId + 2, MinSessionId + 3);
    }

    [Test]
    public void Generate_WhenIdsInUse_ShouldSkipThem()
    {
        // Arrange — occupy the two ids the walk would hit after the first issue
        var sut = new SessionGenerator(_worldState);
        RegisterPlayer(MinSessionId + 1);
        RegisterPlayer(MinSessionId + 2);

        // Act
        var ids = Enumerable.Range(0, 3).Select(_ => sut.Generate()).ToList();

        // Assert
        ids.Should().Equal(MinSessionId, MinSessionId + 3, MinSessionId + 4);
    }

    [Test]
    public void Generate_WhenCounterReachesMax_ShouldWrapToMin()
    {
        // Arrange
        var sut = new SessionGenerator(_worldState);
        var range = MaxSessionId - MinSessionId + 1;

        // Act — a full cycle of ids, then one more
        var ids = Enumerable.Range(0, range + 1).Select(_ => sut.Generate()).ToList();

        // Assert — every id in the cycle is distinct and inside the range, and the
        // surplus call wrapped back to the minimum
        ids.Take(range).Should().OnlyContain(id => id >= MinSessionId && id <= MaxSessionId);
        ids.Take(range).Distinct().Should().HaveCount(range);
        ids.Last().Should().Be(MinSessionId);
    }

    private void RegisterPlayer(int sessionId)
    {
        var (player, _) = FakePlayer.Create($"Ghost{sessionId}", sessionId);
        _worldState.TryAddPlayer(sessionId, player).Should().BeTrue();
    }

    private static WorldState CreateWorldState()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Maps.Returns(Array.Empty<MapWithId>());
        return new WorldState(dataFiles, null!, NullLogger<WorldState>.Instance);
    }
}
