using Acorn.Infrastructure;
using Acorn.Options;
using Acorn.Shared.Models.Online;
using FluentAssertions;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     The character cache loop advertises Acornbot with a synthetic record while
///     the bot is enabled, so the online-players list can show the discovery hint.
/// </summary>
public class CharacterCacheHostedServiceAcornbotTests
{
    [Test]
    public void CreateAcornbotRecord_WhenEnabled_CarriesHandleAndHint()
    {
        var record = CharacterCacheHostedService.CreateAcornbotRecordIfEnabled(
            new AcornbotOptions { Enabled = true, Name = "Acornbot" });

        record.Should().NotBeNull();
        record!.Name.Should().Be("Acornbot");
        record.Title.Should().Be(AcornbotPresence.OnlineListTitle);
        record.SessionId.Should().Be(-1, "the sentinel must never collide with a real player session");
        record.Admin.Should().Be("Player");
    }

    [Test]
    public void CreateAcornbotRecord_HonoursConfiguredName()
    {
        var record = CharacterCacheHostedService.CreateAcornbotRecordIfEnabled(
            new AcornbotOptions { Enabled = true, Name = "Helper" });

        record!.Name.Should().Be("Helper");
    }

    [Test]
    public void CreateAcornbotRecord_WhenDisabled_ReturnsNull()
    {
        var record = CharacterCacheHostedService.CreateAcornbotRecordIfEnabled(
            new AcornbotOptions { Enabled = false });

        record.Should().BeNull("a disabled bot must not advertise itself; the cached entry expires");
    }
}
