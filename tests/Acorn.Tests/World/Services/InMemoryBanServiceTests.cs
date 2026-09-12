using Acorn.World.Services.Bans;
using FluentAssertions;
using Xunit;

namespace Acorn.Tests.World.Services;

public class InMemoryBanServiceTests
{
    private readonly InMemoryBanService _sut = new();

    [Fact]
    public void IsBanned_WhenKeyNeverBanned_ShouldReturnFalse()
    {
        _sut.IsBanned(BanKeys.Username("someone")).Should().BeFalse();
    }

    [Fact]
    public void Ban_ThenIsBanned_ShouldReturnTrue()
    {
        var key = BanKeys.Username("someone");

        _sut.Ban(key);

        _sut.IsBanned(key).Should().BeTrue();
    }

    [Fact]
    public void IsBanned_ShouldBeCaseInsensitive()
    {
        _sut.Ban(BanKeys.Username("SomeOne"));

        _sut.IsBanned(BanKeys.Username("someone")).Should().BeTrue();
    }

    [Fact]
    public void IsBanned_WhenTemporaryBanExpired_ShouldReturnFalse()
    {
        var key = BanKeys.Hdid("abc");
        _sut.Ban(key, DateTime.UtcNow.AddMilliseconds(-1));

        _sut.IsBanned(key).Should().BeFalse();
    }

    [Fact]
    public void IsBanned_WhenTemporaryBanActive_ShouldReturnTrue()
    {
        var key = BanKeys.Hdid("abc");
        _sut.Ban(key, DateTime.UtcNow.AddMinutes(5));

        _sut.IsBanned(key).Should().BeTrue();
    }

    [Fact]
    public void Unban_WhenBanned_ShouldRemoveAndReturnTrue()
    {
        var key = BanKeys.Username("someone");
        _sut.Ban(key);

        _sut.Unban(key).Should().BeTrue();
        _sut.IsBanned(key).Should().BeFalse();
    }

    [Fact]
    public void Unban_WhenNotBanned_ShouldReturnFalse()
    {
        _sut.Unban(BanKeys.Username("someone")).Should().BeFalse();
    }

    [Fact]
    public void GetActiveBans_ShouldExcludeExpiredEntries()
    {
        _sut.Ban(BanKeys.Username("active"));
        _sut.Ban(BanKeys.Username("expired"), DateTime.UtcNow.AddSeconds(-1));

        _sut.GetActiveBans().Should().ContainSingle()
            .Which.Should().Be(BanKeys.Username("active"));
    }

    [Fact]
    public void DifferentKeyTypes_ShouldNotCollide()
    {
        _sut.Ban(BanKeys.Username("abc"));

        _sut.IsBanned(BanKeys.Hdid("abc")).Should().BeFalse();
        _sut.IsBanned(BanKeys.Ip("abc")).Should().BeFalse();
    }
}
