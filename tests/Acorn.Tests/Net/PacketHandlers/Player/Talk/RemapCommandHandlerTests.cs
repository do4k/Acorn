using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.Tests.Game.Services;
using Acorn.Tests.TestHelpers;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $remap must reload a single map file through the data repository and swap it
///     into the world - but never while players are still inside it.
/// </summary>
public class RemapCommandHandlerTests
{
    private const int MapId = 5;

    /// <summary>
    ///     Minimal file repository whose <c>TryReloadMap</c> swaps a canned map in,
    ///     so tests can exercise the real <see cref="WorldState.TryReplaceMap" />
    ///     without touching disk.
    /// </summary>
    private sealed class StubDataFiles : IDataFileRepository
    {
        private readonly List<MapWithId> _maps;

        public StubDataFiles(params MapWithId[] maps)
        {
            _maps = [.. maps];
        }

        public Ecf Ecf { get; } = new();
        public Eif Eif { get; } = new();
        public Enf Enf { get; } = new();
        public Esf Esf { get; } = new();
        public IEnumerable<MapWithId> Maps => _maps;

        /// <summary>The record <see cref="TryReloadMap" /> loads, or null to simulate a missing file.</summary>
        public MapWithId? FreshMap { get; set; }

        public int? LastReloadedMapId { get; private set; }

        public void Reload()
        {
        }

        public bool TryReloadMap(int mapId, out MapWithId? map)
        {
            map = null;
            if (FreshMap is null || FreshMap.Id != mapId) return false;

            LastReloadedMapId = mapId;
            _maps.RemoveAll(m => m.Id != mapId);
            _maps.Add(FreshMap);
            map = FreshMap;
            return true;
        }
    }

    private static (RemapCommandHandler Sut, StubDataFiles Data, WorldState World, INotificationService
        Notifications, MapState Original) CreateSut()
    {
        var data = new StubDataFiles(new MapWithId(MapId, MapTestData.CreateEmf()));

        var factory = new MapStateFactory(
            data,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<INpcController>(),
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            GameTestFactory.ServerOptions(),
            OptionsFactory.Create(new ArenaOptions()),
            NullLogger<MapState>.Instance);

        var world = new WorldState(data, factory, NullLogger<WorldState>.Instance);
        var notifications = Substitute.For<INotificationService>();

        return (new RemapCommandHandler(data, world, notifications), data, world, notifications,
            world.MapForId(MapId)!);
    }

    [Test]
    public async Task Remap_EmptyMap_ReplacesWorldState()
    {
        // Arrange
        var (sut, data, world, notifications, original) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        var freshEmf = MapTestData.CreateEmf();
        freshEmf.Name = "Fresh";
        data.FreshMap = new MapWithId(MapId, freshEmf);

        // Act
        await sut.HandleAsync(admin, "remap", "5");

        // Assert
        data.LastReloadedMapId.Should().Be(MapId);
        world.MapForId(MapId).Should().NotBeSameAs(original, "the map state must be rebuilt from fresh data");
        world.MapForId(MapId)!.Data.Name.Should().Be("Fresh");
        await notifications.Received(1).SystemMessage(admin, Arg.Is<string>(s => s.Contains("reloaded")));
    }

    [Test]
    public async Task Remap_MapWithPlayers_RefusesAndKeepsOriginalState()
    {
        // Arrange
        var (sut, data, world, notifications, original) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        var (occ, _) = FakePlayer.Create("Occ", 2);
        original.Players.TryAdd(2, occ);

        var freshEmf = MapTestData.CreateEmf();
        freshEmf.Name = "Fresh";
        data.FreshMap = new MapWithId(MapId, freshEmf);

        // Act
        await sut.HandleAsync(admin, "remap", "5");

        // Assert
        world.MapForId(MapId).Should().BeSameAs(original);
        await notifications.Received(1).SystemMessage(admin, Arg.Is<string>(s => s.Contains("not empty")));
    }

    [Test]
    public async Task Remap_MissingFile_ReportsNotFound()
    {
        // Arrange
        var (sut, data, world, notifications, original) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        data.FreshMap = null;

        // Act
        await sut.HandleAsync(admin, "remap", "5");

        // Assert
        data.LastReloadedMapId.Should().BeNull();
        world.MapForId(MapId).Should().BeSameAs(original);
        await notifications.Received(1).SystemMessage(admin, Arg.Is<string>(s => s.Contains("not found")));
    }

    [Test]
    public async Task Remap_NoArguments_ShowsUsage()
    {
        // Arrange
        var (sut, _, _, notifications, _) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);

        // Act
        await sut.HandleAsync(admin, "remap");

        // Assert
        await notifications.Received(1).SystemMessage(admin, Arg.Is<string>(s => s.StartsWith("Usage:")));
    }
}
