using System.IO;
using Acorn.Net.PacketHandlers.Player;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Tests.Game.Player;

public class WelcomeFlowTests
{
    [Test]
    public void BuildNews_WhenNewsFileHasMoreThanMaxLines_ShouldNotThrowAndClamp()
    {
        // Regression test for issue #45: Enumerable.Range(0, 8 - length) threw when the
        // news file had more than 8 lines, dropping the connection.
        var lines = Enumerable.Range(1, 20).Select(i => $"news {i}").ToArray();

        var act = () => WelcomeMsgClientPacketHandler.BuildNews(lines);

        var result = act.Should().NotThrow().Subject;
        result.Should().HaveCount(WelcomeMsgClientPacketHandler.MaxNewsLines + 1);
        result[0].Should().Be(" ");
        result[1].Should().Be("news 1");
        result[WelcomeMsgClientPacketHandler.MaxNewsLines]
            .Should().Be($"news {WelcomeMsgClientPacketHandler.MaxNewsLines}");
    }

    [Test]
    public void BuildNews_WhenFewerLinesThanMax_ShouldPadWithEmptyStrings()
    {
        var result = WelcomeMsgClientPacketHandler.BuildNews(["first", "second"]);

        result.Should().HaveCount(WelcomeMsgClientPacketHandler.MaxNewsLines + 1);
        result[0].Should().Be(" ");
        result[1].Should().Be("first");
        result[2].Should().Be("second");
        result.Skip(3).Should().OnlyContain(line => line == "");
    }

    [Test]
    public void BuildNews_WhenEmpty_ShouldReturnMotdSpacerAndEmptyLines()
    {
        var result = WelcomeMsgClientPacketHandler.BuildNews([]);

        result.Should().HaveCount(WelcomeMsgClientPacketHandler.MaxNewsLines + 1);
        result[0].Should().Be(" ");
        result.Skip(1).Should().OnlyContain(line => line == "");
    }

    [Test]
    public void LoadNews_WhenFileMissing_ShouldReturnEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        var result = WelcomeMsgClientPacketHandler.LoadNews(missing);

        result.Should().BeEmpty();
    }

    [Test]
    public void LoadNews_WhenFileHasManyLines_ShouldNotThrowWhenBuilding()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, Enumerable.Range(0, 50).Select(i => $"line {i}"));

            var lines = WelcomeMsgClientPacketHandler.LoadNews(path);
            lines.Should().HaveCount(50);

            var act = () => WelcomeMsgClientPacketHandler.BuildNews(lines);
            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(AdminLevel.Player, AdminLevel.Player)]
    [Arguments(AdminLevel.Spy, AdminLevel.Spy)]
    [Arguments(AdminLevel.LightGuide, AdminLevel.LightGuide)]
    [Arguments(AdminLevel.Guardian, AdminLevel.HighGameMaster)]
    [Arguments(AdminLevel.GameMaster, AdminLevel.HighGameMaster)]
    [Arguments(AdminLevel.HighGameMaster, AdminLevel.HighGameMaster)]
    public void GetClientAdminLevel_ShouldElevateStaffToUnlockAdminUi(AdminLevel actual, AdminLevel expected)
    {
        WelcomeRequestClientPacketHandler.GetClientAdminLevel(actual).Should().Be(expected);
    }

    [Test]
    public void GetRequestedFileId_WhenPubFile_ShouldReturnRequestedId()
    {
        var data = new WelcomeAgreeClientPacket.FileTypeDataEif { FileId = 3 };

        WelcomeAgreeClientPacketHandler.GetRequestedFileId(data).Should().Be(3);
    }

    [Test]
    public void GetRequestedFileId_WhenMapOrNull_ShouldDefaultToOne()
    {
        WelcomeAgreeClientPacketHandler.GetRequestedFileId(null).Should().Be(1);
        WelcomeAgreeClientPacketHandler.GetRequestedFileId(new WelcomeAgreeClientPacket.FileTypeDataEmf { FileId = 5 })
            .Should().Be(1);
    }
}