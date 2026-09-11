using Acorn.Game.Mappers;
using Acorn.Tests.TestHelpers;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Game.Mappers;

public class StatSkillPacketMapperTests
{
    [Fact]
    public void ToSkillAccept_ShouldUseAcceptActionAndCarryPointsSpellAndLevel()
    {
        var packet = StatSkillPacketMapper.ToSkillAccept(skillPoints: 12, spellId: 7, level: 3);

        packet.Family.Should().Be(PacketFamily.StatSkill);
        packet.Action.Should().Be(PacketAction.Accept);
        packet.SkillPoints.Should().Be(12);
        packet.Spell.Id.Should().Be(7);
        packet.Spell.Level.Should().Be(3);
    }

    [Fact]
    public void ToStatPlayer_ShouldUsePlayerActionAndCarryStatPoints()
    {
        var character = GameTestFactory.Character(statPoints: 4);
        character.Str = 5;
        character.AdjStr = 7;

        var packet = StatSkillPacketMapper.ToStatPlayer(character);

        packet.Family.Should().Be(PacketFamily.StatSkill);
        packet.Action.Should().Be(PacketAction.Player);
        packet.StatPoints.Should().Be(4);
        packet.Stats.BaseStats.Str.Should().Be(7);
    }

    [Fact]
    public void ToWrongClassReply_ShouldUseReplyActionAndCarryClassId()
    {
        var packet = StatSkillPacketMapper.ToWrongClassReply(classId: 2);

        packet.Family.Should().Be(PacketFamily.StatSkill);
        packet.Action.Should().Be(PacketAction.Reply);
        packet.ReplyCode.Should().Be(SkillMasterReply.WrongClass);
        packet.ReplyCodeData.Should()
            .BeOfType<StatSkillReplyServerPacket.ReplyCodeDataWrongClass>()
            .Which.ClassId.Should().Be(2);
    }
}
