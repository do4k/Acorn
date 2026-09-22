using Acorn.Game.Services;
using Acorn.Options;
using FluentAssertions;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     The banned-symbol policy is a simple global substring deny list, matched
///     case-insensitively so one configuration can gate names, guild text and titles.
/// </summary>
public class BannedTextPolicyTests
{
    private static BannedTextPolicy CreateSut(params string[] symbols)
    {
        var options = OptionsFactory.Create(new BannedTextOptions { Symbols = [.. symbols] });
        return new BannedTextPolicy(options);
    }

    [Test]
    public void FirstViolation_WhenEmptyList_AllowsEverything()
    {
        var sut = CreateSut();

        sut.FirstViolation("weird # title $ with ~ symbols").Should().BeNull();
    }

    [Test]
    public void FirstViolation_MatchesCaseInsensitively()
    {
        var sut = CreateSut("Ab");

        sut.FirstViolation("xAby").Should().Be("Ab");
    }

    [Test]
    public void FirstViolation_ReturnsFirstConfiguredMatch()
    {
        // Both symbols appear in the text; the configured list order wins.
        var sut = CreateSut("$", "#");

        sut.FirstViolation("a$b#").Should().Be("$");
    }

    [Test]
    public void FirstViolation_NullOrEmptyText_IsAllowed()
    {
        var sut = CreateSut("#");

        sut.FirstViolation(null).Should().BeNull();
        sut.FirstViolation(string.Empty).Should().BeNull();
    }

    [Test]
    public void FirstViolation_IgnoresBlankConfiguredEntries()
    {
        var sut = CreateSut("", " ", "#");

        sut.FirstViolation("plain title").Should().BeNull();
        sut.FirstViolation("has#hash").Should().Be("#");
    }
}
