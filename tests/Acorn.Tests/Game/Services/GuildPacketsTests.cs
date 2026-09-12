using Acorn.World.Services.Guild;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Tests.Game.Services;

public class GuildPacketsTests
{
    [Test]
    public void CreateBegin_ShouldUseCreateBeginReplyCode()
    {
        var packet = GuildPackets.CreateBegin();

        packet.ReplyCode.Should().Be(GuildReply.CreateBegin);
        packet.ReplyCodeData.Should().BeNull();
    }

    [Test]
    public void CreateInvite_ShouldIncludeLeaderIdAndGuildIdentity()
    {
        var packet = GuildPackets.CreateInvite(leaderPlayerId: 7, guildName: "my guild", guildTag: "abc");

        packet.PlayerId.Should().Be(7);
        packet.GuildIdentity.Should().Be("My guild (ABC)");
    }

    [Test]
    public void CreateAdd_ShouldIncludeMemberName()
    {
        var packet = GuildPackets.CreateAdd("Bob");

        packet.ReplyCode.Should().Be(GuildReply.CreateAdd);
        packet.ReplyCodeData.Should().BeOfType<GuildReplyServerPacket.ReplyCodeDataCreateAdd>()
            .Which.Name.Should().Be("Bob");
    }

    [Test]
    public void CreateAddConfirm_ShouldIncludeMemberName()
    {
        var packet = GuildPackets.CreateAddConfirm("Bob");

        packet.ReplyCode.Should().Be(GuildReply.CreateAddConfirm);
        packet.ReplyCodeData.Should().BeOfType<GuildReplyServerPacket.ReplyCodeDataCreateAddConfirm>()
            .Which.Name.Should().Be("Bob");
    }

    [Test]
    public void JoinRequest_ShouldIncludePlayerIdAndName()
    {
        var packet = GuildPackets.JoinRequest(42, "Alice");

        packet.ReplyCode.Should().Be(GuildReply.JoinRequest);
        var data = packet.ReplyCodeData.Should().BeOfType<GuildReplyServerPacket.ReplyCodeDataJoinRequest>().Which;
        data.PlayerId.Should().Be(42);
        data.Name.Should().Be("Alice");
    }

    [Test]
    public void Reply_ShouldHaveNoData()
    {
        var packet = GuildPackets.Reply(GuildReply.NotFound);

        packet.ReplyCode.Should().Be(GuildReply.NotFound);
        packet.ReplyCodeData.Should().BeNull();
    }
}