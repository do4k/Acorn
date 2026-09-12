using Acorn.Net;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Tests.Infrastructure;

public class GuildRateLimitTests
{
    [Test]
    [Arguments(PacketAction.Request, 1000)]
    [Arguments(PacketAction.Create, 1000)]
    [Arguments(PacketAction.Player, 500)]
    [Arguments(PacketAction.Tell, 500)]
    [Arguments(PacketAction.Report, 500)]
    public void DefaultLimits_ShouldContainGuildRateLimit(PacketAction action, int limitMs)
    {
        PacketRateLimits.DefaultLimits
            .Should().Contain(l => l.Family == PacketFamily.Guild && l.Action == action && l.LimitMs == limitMs);
    }

    [Test]
    public void PacketLog_ShouldRateLimitGuildRequestAfterItIsRecorded()
    {
        var log = new PacketLog();

        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Guild).Should().BeFalse();
        log.RecordPacket(PacketAction.Request, PacketFamily.Guild);
        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Guild).Should().BeTrue();
    }

    [Test]
    public void PacketLog_ShouldNotRateLimitDifferentGuildAction()
    {
        var log = new PacketLog();

        log.RecordPacket(PacketAction.Request, PacketFamily.Guild);

        log.ShouldRateLimit(PacketAction.Tell, PacketFamily.Guild).Should().BeFalse();
    }
}