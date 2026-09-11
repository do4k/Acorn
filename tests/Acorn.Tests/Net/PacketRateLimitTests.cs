using Acorn.Net;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Xunit;

namespace Acorn.Tests.Net;

public class PacketRateLimitTests
{
    [Theory]
    [InlineData(PacketAction.Request)]
    [InlineData(PacketAction.Accept)]
    [InlineData(PacketAction.Remove)]
    [InlineData(PacketAction.Take)]
    public void DefaultLimits_ShouldRateLimitPartyPackets(PacketAction action)
    {
        PacketRateLimits.DefaultLimits
            .Should().Contain(l => l.Family == PacketFamily.Party && l.Action == action,
                "party packets must be rate limited to prevent invite/accept spam");
    }

    [Fact]
    public void ShouldRateLimit_PartyRequest_WhenSentTwiceImmediately_ShouldReturnTrue()
    {
        var log = new PacketLog();

        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Party).Should().BeFalse("the first request is allowed");

        log.RecordPacket(PacketAction.Request, PacketFamily.Party);

        log.ShouldRateLimit(PacketAction.Request, PacketFamily.Party)
            .Should().BeTrue("a second request within 500ms must be blocked");
    }

    [Fact]
    public void ShouldRateLimit_UnlistedPacket_ShouldReturnFalse()
    {
        var log = new PacketLog();
        log.RecordPacket(PacketAction.Create, PacketFamily.Party);

        log.ShouldRateLimit(PacketAction.Create, PacketFamily.Party).Should().BeFalse();
    }
}
