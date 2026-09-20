using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Ground item lifecycle. NPC drops must be stamped with the current tick so
///     the cleanup pass does not expire them immediately (regression for #101).
/// </summary>
public class MapItemServiceTests
{
    private const int ItemCleanupTicks = 300;

    private static MapItemService CreateService()
    {
        return new MapItemService(
            Substitute.For<IInventoryService>(),
            Substitute.For<IWeightCalculator>(),
            Substitute.For<IDataFileRepository>(),
            Substitute.For<IMapTileService>(),
            Substitute.For<IMapBroadcastService>(),
            NullLogger<MapItemService>.Instance);
    }

    private static MapController CreateController()
    {
        return new MapController(
            Substitute.For<IMapTileService>(),
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<INpcCombatService>(),
            Substitute.For<INpcController>(),
            Substitute.For<IPlayerController>(),
            Substitute.For<IFormulaService>(),
            NullLogger<MapController>.Instance,
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            new Lazy<WorldState>(() => null!));
    }

    [Test]
    public void AddGroundItem_ShouldStampDroppedAtTickAndStoreOnMap()
    {
        var map = FakeMap.Create();

        var (index, item) = CreateService().AddGroundItem(
            map, itemId: 5, amount: 1, new Coords { X = 1, Y = 1 }, ownerId: 42, protectionTicks: 60);

        map.Items.Should().ContainKey(index);
        map.Items[index].Should().BeSameAs(item);
        item.DroppedAtTick.Should().Be(map.TotalTicks);
    }

    [Test]
    public void ProcessGroundItemCleanup_ShouldKeepFreshItemsAndRemoveExpiredOnes()
    {
        var map = FakeMap.Create();
        var currentTick = map.TotalTicks;

        map.Items[1] = new MapItem
        {
            Id = 1, Amount = 1, Coords = new Coords(), DroppedAtTick = currentTick
        };
        map.Items[2] = new MapItem
        {
            Id = 2, Amount = 1, Coords = new Coords(), DroppedAtTick = currentTick - ItemCleanupTicks
        };

        CreateController().ProcessGroundItemCleanup(map);

        map.Items.Should().ContainKey(1);
        map.Items.Should().NotContainKey(2);
    }

    [Test]
    public void AddGroundItem_ThenCleanup_ShouldSurviveOnALongRunningMap()
    {
        // The original bug: NPC drops were created without DroppedAtTick (default 0),
        // so on a map whose tick counter had passed the cleanup threshold they were
        // removed on the very next cleanup pass.
        var map = FakeMap.Create();
        for (var i = 0; i < ItemCleanupTicks + 5; i++)
        {
            map.Tick().GetAwaiter().GetResult();
        }

        var (index, _) = CreateService().AddGroundItem(
            map, itemId: 5, amount: 1, new Coords { X = 1, Y = 1 }, ownerId: 42, protectionTicks: 60);

        CreateController().ProcessGroundItemCleanup(map);

        map.Items.Should().ContainKey(index);
    }
}
