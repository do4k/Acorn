using Acorn.Shared.Caching;
using Acorn.Shared.Models.Online;
using FluentAssertions;

namespace Acorn.Tests.Extensions;

/// <summary>
///     The online-character cache feeds the REST summaries; titles (including
///     Acornbot's "!acornbot help" hint) must survive into the player summary.
/// </summary>
public class CharacterCacheServiceOnlineListTests
{
    [Test]
    public async Task GetOnlinePlayers_CarriesTitlesIntoSummaries()
    {
        // Arrange
        var sut = new CharacterCacheService(new InMemoryCacheService());
        await sut.CacheCharacterAsync(new OnlineCharacterRecord
        {
            SessionId = 7,
            Name = "Zany",
            Title = "Extra"
        });

        // Act
        var online = await sut.GetOnlinePlayersAsync();

        // Assert
        online.TotalOnline.Should().Be(1);
        online.Players.Single().Name.Should().Be("Zany");
        online.Players.Single().Title.Should().Be("Extra");
    }
}
