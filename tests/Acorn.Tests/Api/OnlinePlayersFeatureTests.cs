using Acorn.Api.Features;
using Acorn.Api.Options;
using Acorn.Shared.Models.Online;
using FluentAssertions;

namespace Acorn.Tests.Api;

/// <summary>
///     /api/online advertises Acornbot while the bot is enabled, with the
///     "!acornbot help" discovery hint as its title, and never double-lists it.
/// </summary>
public class OnlinePlayersFeatureTests
{
    private static OnlinePlayersRecord With(params OnlinePlayerSummary[] players) =>
        new() { TotalOnline = players.Length, Players = [.. players] };

    private static readonly AcornbotOptions Enabled = new() { Enabled = true, Name = "Acornbot" };

    [Test]
    public void WithAcornbot_WhenEnabled_PrependsBotAndCountsIt()
    {
        var players = With(new OnlinePlayerSummary { Name = "danzo", Level = 3 });

        var result = OnlinePlayersFeature.WithAcornbot(players, Enabled);

        result.TotalOnline.Should().Be(2);
        result.Players[0].Name.Should().Be("Acornbot");
        result.Players[0].Title.Should().Be("!acornbot help");
        result.Players[1].Name.Should().Be("danzo");
    }

    [Test]
    public void WithAcornbot_WhenDisabled_ListUnchanged()
    {
        var players = With(new OnlinePlayerSummary { Name = "danzo" });

        var result = OnlinePlayersFeature.WithAcornbot(players, new AcornbotOptions { Enabled = false });

        result.Should().BeSameAs(players);
    }

    [Test]
    public void WithAcornbot_BotAlreadyInCache_IsNotDuplicated()
    {
        // Future shared-cache runs will include the server-seeded entry; the name
        // check keeps exactly one Acornbot in the list.
        var players = With(new OnlinePlayerSummary { Name = "Acornbot", Title = "!acornbot help" });

        var result = OnlinePlayersFeature.WithAcornbot(players, Enabled);

        result.Should().BeSameAs(players);
    }

    [Test]
    public void WithAcornbot_MatchesNameCaseInsensitively()
    {
        var players = With(new OnlinePlayerSummary { Name = "acornbot" });

        var result = OnlinePlayersFeature.WithAcornbot(players, Enabled);

        result.Should().BeSameAs(players);
    }

    [Test]
    public void WithAcornbot_EmptyName_LeavesListUnchanged()
    {
        var players = With(new OnlinePlayerSummary { Name = "danzo" });

        var result = OnlinePlayersFeature.WithAcornbot(players, new AcornbotOptions { Enabled = true, Name = "  " });

        result.Should().BeSameAs(players);
    }
}
