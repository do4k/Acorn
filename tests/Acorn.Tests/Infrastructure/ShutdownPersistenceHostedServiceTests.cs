using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Infrastructure;
using Acorn.Options;
using Acorn.Tests.Game.Services;
using Acorn.Tests.TestHelpers;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using GameCharacter = Acorn.Game.Models.Character;
using DbCharacter = Acorn.Database.Models.Character;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     On host shutdown every online character (and their quest progress) must be
///     flushed to the database, even if the sockets never run disconnect cleanup.
/// </summary>
public class ShutdownPersistenceHostedServiceTests
{
    private static WorldState CreateWorld()
    {
        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Maps.Returns([]);
        var factory = new MapStateFactory(
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<INpcController>(),
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            GameTestFactory.ServerOptions(),
            OptionsFactory.Create(new ArenaOptions()),
            NullLogger<MapState>.Instance);

        return new WorldState(dataRepository, factory, NullLogger<WorldState>.Instance);
    }

    private static (ShutdownPersistenceHostedService Sut, IDbRepository<DbCharacter> Characters, IQuestService Quests)
        CreateSut(WorldState world)
    {
        var characters = Substitute.For<IDbRepository<DbCharacter>>();
        var mapper = Substitute.For<ICharacterMapper>();
        mapper.ToDatabase(Arg.Any<GameCharacter>()).Returns(new DbCharacter
        {
            Accounts_Username = "tester",
            Name = "Persisted"
        });
        var quests = Substitute.For<IQuestService>();

        var services = new ServiceCollection();
        services.AddSingleton(characters);
        services.AddSingleton(mapper);
        services.AddSingleton(quests);
        var provider = services.BuildServiceProvider();

        var sut = new ShutdownPersistenceHostedService(world, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShutdownPersistenceHostedService>.Instance);

        return (sut, characters, quests);
    }

    [Test]
    public async Task Stop_PersistsEveryOnlineCharacter()
    {
        // Arrange
        var world = CreateWorld();
        var (player, _) = FakePlayer.Create("Persisted", 1);
        world.TryAddPlayer(1, player);
        var (sut, characters, quests) = CreateSut(world);

        // Act: starting then stopping drives the persist-on-cancel path
        await sut.StartAsync(default);
        await sut.StopAsync(default);

        // Assert
        await characters.Received(1).UpdateAsync(Arg.Any<DbCharacter>());
        await quests.Received(1).SaveQuestProgress(player.Character!);
    }

    [Test]
    public async Task Stop_SkipsConnectionsWithoutCharacter()
    {
        // Arrange
        var world = CreateWorld();
        var (player, _) = FakePlayer.Create("Bare", 1);
        player.Character = null!;
        world.TryAddPlayer(1, player);
        var (sut, characters, quests) = CreateSut(world);

        // Act
        await sut.StartAsync(default);
        await sut.StopAsync(default);

        // Assert
        await characters.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await quests.DidNotReceiveWithAnyArgs().SaveQuestProgress(default!);
    }
}
