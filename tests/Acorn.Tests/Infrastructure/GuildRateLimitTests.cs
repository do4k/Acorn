using Acorn.Net;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Xunit;

namespace Acorn.Tests.Infrastructure;

public class GuildRateLimitTests
{
    [Theory]
    [InlineData(PacketAction.Request, 1000)]
    [InlineData(PacketAction.Create, 1000)]
    [InlineData(PacketAction.Player, 500)]
    [InlineData(PacketAction.Tell, 500)]
    [InlineData(PacketAction.Report, 500)]
    public void DefaultLimits_ShouldContainGuildRateLimit(PacketAction action, int limitMs)
    {
        PacketRateLimits.DefaultLimits
            .Should().Contain(l => l.Family == PacketFamily.Guild && l.Action == action && l.LimitMs == limitMs);
    }

    [Fact]
    public void PacketLog_ShouldRateLimitGuildRequestAfterItIsRecorded()
    {
        var log = new PacketLog();

        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Guild).Should().BeFalse();
        log.RecordPacket(PacketAction.Request, PacketFamily.Guild);
        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Guild).Should().BeTrue();
    }

    [Fact]
    public void PacketLog_ShouldNotRateLimitDifferentGuildAction()
    {
        var log = new PacketLog();

        log.RecordPacket(PacketAction.Request, PacketFamily.Guild);

        log.ShouldRateLimit(PacketAction.Tell, PacketFamily.Guild).Should().BeFalse();
    }
}
