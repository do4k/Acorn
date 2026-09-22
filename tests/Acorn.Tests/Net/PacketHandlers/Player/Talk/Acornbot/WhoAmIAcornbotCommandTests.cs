using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk.Acornbot;
using Acorn.Tests.TestHelpers;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using NSubstitute;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     whoami is the bot's read-only self summary: identity, location, vitals
///     and resources - delivered as whispers, with no side effects.
/// </summary>
public class WhoAmIAcornbotCommandTests
{
    private static (WhoAmIAcornbotCommand Sut, IAcornbotReplyChannel Replies, PlayerState Player) CreateSut(
        int gold = 1234)
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Ecf.Returns(GameTestFactory.Ecf()); // one class: "Peasant"

        var inventory = Substitute.For<IInventoryService>();
        inventory.GetItemAmount(Arg.Any<Acorn.Game.Models.Character>(), 1).Returns(gold);

        var replies = Substitute.For<IAcornbotReplyChannel>();
        var (player, _) = FakePlayer.Create("Tester");
        player.Character!.Level = 12;
        player.Character.Class = 1;
        player.Character.Title = "Distinguished";
        player.Character.Hp = 80;
        player.Character.MaxHp = 100;

        var sut = new WhoAmIAcornbotCommand(replies, inventory, dataFiles);
        return (sut, replies, player);
    }

    [Test]
    public async Task WhoAmI_SummarisesIdentityAndResources()
    {
        // Arrange
        var (sut, replies, player) = CreateSut();

        // Act
        await sut.HandleAsync(player, "whoami");

        // Assert - four whisper lines, no other side effects
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("Tester") && m.Contains("\"Distinguished\"")
                                && m.Contains("level 12") && m.Contains("Peasant")));
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("Location:")));
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("80/100 hp")));
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("1,234 gold")));
    }

    [Test]
    public async Task WhoAmI_WithoutTitle_SaysNoTitle()
    {
        var (sut, replies, player) = CreateSut(gold: 0);
        player.Character!.Title = null;

        await sut.HandleAsync(player, "me");

        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("no title")));
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("0 gold")));
    }

    [Test]
    public async Task WhoAmI_UnknownClassId_FallsBackToNumericClass()
    {
        var (sut, replies, player) = CreateSut();
        player.Character!.Class = 99;

        await sut.HandleAsync(player, "whoami");

        await replies.Received(1).WhisperAsync(player, Arg.Is<string>(m => m.Contains("Class 99")));
    }
}
