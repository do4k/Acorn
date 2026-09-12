using Acorn.Infrastructure;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

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
    private readonly SessionGenerator _sut = new();

    // AccountReply reserved values that must never be used as a session ID.
    private static readonly int[] ReservedReplyCodes =
        Enum.GetValues<AccountReply>().Select(v => (int)v).ToArray();

    [Test]
    public void Generate_WhenCalledManyTimes_ShouldNeverCollideWithAccountReplyReservedValues()
    {
        // Arrange
        var reservedMin = ReservedReplyCodes.Min();
        var reservedMax = ReservedReplyCodes.Max();

        // Act
        var ids = Enumerable.Range(0, 10_000).Select(_ => _sut.Generate()).ToList();

        // Assert — none may fall inside the reserved AccountReply range [1..7]
        ids.Should().OnlyContain(id => id < reservedMin || id > reservedMax);
    }

    [Test]
    public void Generate_WhenCalledManyTimes_ShouldStayWithinSessionIdRange()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => _sut.Generate()).ToList();

        ids.Should().OnlyContain(id => id >= 8);
        ids.Should().OnlyContain(id => id < (int)EoNumericLimits.SHORT_MAX);
    }

    [Test]
    public void Generate_ShouldNeverEqualAnyAccountReplyEnumValue()
    {
        for (var i = 0; i < 10_000; i++)
        {
            var id = _sut.Generate();
            ReservedReplyCodes.Should().NotContain(id);
        }
    }
}