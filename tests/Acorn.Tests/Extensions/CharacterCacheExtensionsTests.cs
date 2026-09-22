using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Online;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using NSubstitute;

namespace Acorn.Tests.Extensions;

/// <summary>
///     The realtime character cache feeds the REST API, so guild membership must
///     be mirrored from the live character into <see cref="OnlineCharacterRecord" />.
/// </summary>
public class CharacterCacheExtensionsTests
{
    [Test]
    public async Task CacheCharacterState_GuildedCharacter_CachesGuildNameAndRank()
    {
        // Arrange
        var cache = Substitute.For<ICharacterCacheService>();
        var paperdollService = Substitute.For<IPaperdollService>();
        var (player, _) = FakePlayer.Create("Guilded");
        player.Character!.GuildName = "Testing";
        player.Character.GuildRankName = "Tester";

        // Act
        await player.CacheCharacterStateAsync(cache, paperdollService);

        // Assert
        await cache.Received(1).CacheCharacterAsync(Arg.Is<OnlineCharacterRecord>(
            r => r.GuildName == "Testing" && r.GuildRank == "Tester"));
    }

    [Test]
    public async Task CacheCharacterState_GuildlessCharacter_CachesEmptyGuildFields()
    {
        // Arrange
        var cache = Substitute.For<ICharacterCacheService>();
        var paperdollService = Substitute.For<IPaperdollService>();
        var (player, _) = FakePlayer.Create("Solo");

        // Act
        await player.CacheCharacterStateAsync(cache, paperdollService);

        // Assert
        await cache.Received(1).CacheCharacterAsync(Arg.Is<OnlineCharacterRecord>(
            r => r.GuildName == string.Empty && r.GuildRank == string.Empty));
    }
}
