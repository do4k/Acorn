using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class ChatSanitizerTests
{
    private static ChatSanitizer CreateSut(int chatLength = 128, int chatMaxWidth = 1400)
    {
        return new ChatSanitizer(Microsoft.Extensions.Options.Options.Create(FakePlayer.CreateOptions(chatLength, chatMaxWidth)));
    }

    [Fact]
    public void Sanitize_WhenMessageWithinLength_ReturnsUnchanged()
    {
        var sut = CreateSut();

        var result = sut.Sanitize("hello world");

        result.Should().Be("hello world");
    }

    [Fact]
    public void Sanitize_WhenMessageExceedsLength_TruncatesWithEllipsis()
    {
        var sut = CreateSut(chatLength: 20);

        var result = sut.Sanitize(new string('a', 50));

        result.Should().HaveLength(20);
        result.Should().EndWith(" [...]");
    }

    [Fact]
    public void Sanitize_WhenMessageExceedsWidth_TruncatesByPixelWidth()
    {
        // 'W' is 11px wide; a 50px budget fits 4 characters.
        var sut = CreateSut(chatLength: 1000, chatMaxWidth: 50);

        var result = sut.Sanitize(new string('W', 20));

        result.Should().HaveLength(4);
        ChatText.Width(result).Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void Sanitize_WhenSenderNameProvided_ReducesWidthBudgetByPrefix()
    {
        // "Bob  " is 25px wide, leaving a 25px budget: two 'W' (22px) fit.
        var sut = CreateSut(chatLength: 1000, chatMaxWidth: 50);

        var result = sut.Sanitize(new string('W', 20), "Bob");

        result.Should().HaveLength(2);
    }

    [Fact]
    public void Cap_WhenWidthIsZero_ReturnsEmpty()
    {
        ChatText.Cap("hello", 0).Should().BeEmpty();
    }

    [Fact]
    public void LimitLength_WhenMaxLengthSixOrLess_TruncatesWithoutEllipsis()
    {
        ChatText.LimitLength("abcdefgh", 4).Should().Be("abcd");
    }

    [Theory]
    [InlineData(Emote.Happy, true)]
    [InlineData(Emote.Sad, true)]
    [InlineData(Emote.Embarrassed, true)]
    [InlineData(Emote.Drunk, true)]
    [InlineData(Emote.Playful, true)]
    [InlineData(Emote.Trade, false)]
    [InlineData(Emote.LevelUp, false)]
    [InlineData(Emote.Bard, false)]
    public void IsValidEmote_OnlyAllowsClientSendableEmotes(Emote emote, bool expected)
    {
        EmoteReportClientPacketHandler.IsValidEmote(emote).Should().Be(expected);
    }
}
