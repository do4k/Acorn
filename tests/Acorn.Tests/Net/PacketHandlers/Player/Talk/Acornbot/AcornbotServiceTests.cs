using Acorn.Net.PacketHandlers.Player.Talk.Acornbot;
using Acorn.Options;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Whispers addressed to the bot name are parsed and dispatched to the
///     registered Acornbot commands; everything else must fall through to
///     normal player lookup (via <see cref="AcornbotService.IsBotName" />).
/// </summary>
public class AcornbotServiceTests
{
    private static (AcornbotService Sut, IAcornbotReplyChannel Replies, IAcornbotCommand Title) CreateSut(
        bool enabled = true,
        string name = "Acornbot")
    {
        var replies = Substitute.For<IAcornbotReplyChannel>();
        replies.BotName.Returns(name);

        var title = Substitute.For<IAcornbotCommand>();
        title.Commands.Returns((IReadOnlyList<string>)["title"]);
        title.Usage.Returns("<title text> | clear");
        title.Description.Returns("Set the title shown under your name, or 'clear' to remove it.");

        var options = OptionsFactory.Create(new AcornbotOptions { Enabled = enabled, Name = name });
        var sut = new AcornbotService([title], replies, options, NullLogger<AcornbotService>.Instance);
        return (sut, replies, title);
    }

    [Test]
    public void IsBotName_WhenEnabledAndNameMatchesCaseInsensitively_True()
    {
        // Arrange / Act
        var (sut, _, _) = CreateSut();

        // Assert - the eoweb client lower-cases whisper targets before sending
        sut.IsBotName("acornbot").Should().BeTrue();
        sut.IsBotName("ACORNBOT").Should().BeTrue();
    }

    [Test]
    public void IsBotName_WhenDisabled_False()
    {
        var (sut, _, _) = CreateSut(enabled: false);

        sut.IsBotName("Acornbot").Should().BeFalse();
    }

    [Test]
    public void IsBotName_OtherPlayer_False()
    {
        var (sut, _, _) = CreateSut();

        sut.IsBotName("SomePlayer").Should().BeFalse();
    }

    [Test]
    public void ReservesName_WhenNameMatches_TrueEvenWhileDisabled()
    {
        var (enabled, _, _) = CreateSut();
        enabled.ReservesName("acornbot").Should().BeTrue("the eoweb client lower-cases the handle");

        // Disabled bots must still reserve their handle so no character can be
        // created that would silently shadow the bot once switched on.
        var (disabled, _, _) = CreateSut(enabled: false);
        disabled.ReservesName("Acornbot").Should().BeTrue();
        disabled.IsBotName("Acornbot").Should().BeFalse();
    }

    [Test]
    public void ReservesName_OtherNames_False()
    {
        var (sut, _, _) = CreateSut();

        sut.ReservesName("SomePlayer").Should().BeFalse();
        sut.ReservesName("Acornbotx").Should().BeFalse();
        sut.ReservesName("").Should().BeFalse();
    }

    [Test]
    public async Task HandleWhisper_PlainCommand_DispatchesToHandler()
    {
        // Arrange
        var (sut, _, title) = CreateSut();
        var (player, _) = FakePlayer.Create("Tester");

        // Act
        await sut.HandleWhisperAsync(player, "title Cool Dude");

        // Assert
        await title.Received(1).HandleAsync(player, "title", "Cool", "Dude");
    }

    [Test]
    public async Task HandleWhisper_RepeatedHandlePrefix_StrippedBeforeDispatch()
    {
        // Arrange - the vanilla PM window sends the raw text including the handle
        var (sut, _, title) = CreateSut();
        var (player, _) = FakePlayer.Create("Tester");

        // Act
        await sut.HandleWhisperAsync(player, "!Acornbot title Cool Dude");

        // Assert
        await title.Received(1).HandleAsync(player, "title", "Cool", "Dude");
    }

    [Test]
    public async Task HandleWhisper_UnknownCommand_RepliesWithHint()
    {
        var (sut, replies, title) = CreateSut();
        var (player, _) = FakePlayer.Create("Tester");

        await sut.HandleWhisperAsync(player, "warp me somewhere");

        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("Unknown command")));
        await title.DidNotReceiveWithAnyArgs().HandleAsync(default!, default!, default!);
    }

    [Test]
    public async Task HandleWhisper_EmptyMessage_ListsCommands()
    {
        var (sut, replies, title) = CreateSut();
        var (player, _) = FakePlayer.Create("Tester");

        await sut.HandleWhisperAsync(player, "");

        await replies.Received(1).WhisperAsync(player, Arg.Is<string>(m => m.Contains("Send me a command")));
        await replies.Received(1).WhisperAsync(player,
            Arg.Is<string>(m => m.Contains("title <title text> | clear") && m.Contains("Set the title")));
        await title.DidNotReceiveWithAnyArgs().HandleAsync(default!, default!, default!);
    }

    [Test]
    public async Task HandleWhisper_HelpKeyword_ListsCommands()
    {
        var (sut, replies, _) = CreateSut();
        var (player, _) = FakePlayer.Create("Tester");

        await sut.HandleWhisperAsync(player, "HELP");

        await replies.Received(1).WhisperAsync(player, Arg.Is<string>(m => m.Contains("Send me a command")));
    }
}
