using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Tests.Support;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Game.Services;

public class MapItemServiceTests
{
    private readonly IMapBroadcastService _broadcastService = Substitute.For<IMapBroadcastService>();
    private readonly IDataFileRepository _dataRepository = Substitute.For<IDataFileRepository>();
    private readonly IInventoryService _inventoryService = Substitute.For<IInventoryService>();
    private readonly MapItemService _sut;
    private readonly MapTileService _tileService = new();
    private readonly IWeightCalculator _weightCalculator = Substitute.For<IWeightCalculator>();

    public MapItemServiceTests()
    {
        _inventoryService.HasItem(Arg.Any<Character>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);
        _inventoryService.TryRemoveItem(Arg.Any<Character>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);

        _sut = new MapItemService(
            _inventoryService,
            _weightCalculator,
            _dataRepository,
            _tileService,
            _broadcastService,
            NullLogger<MapItemService>.Instance);
    }

    [Test]
    public async Task TryDropItem_WhenSuccessful_AddsItemToMapAndRemovesFromInventory()
    {
        var map = TestFactories.CreateMap();
        var player = TestFactories.CreatePlayer(1, 5, 5);
        map.Players[player.SessionId] = player;

        var result = await _sut.TryDropItem(player, map, itemId: 5, amount: 3, new Coords { X = 5, Y = 5 });

        result.Success.Should().BeTrue();
        result.ItemIndex.Should().Be(1);
        map.Items.Should().ContainKey(1);
        map.Items[1].Id.Should().Be(5);
        map.Items[1].Amount.Should().Be(3);
        _inventoryService.Received(1).TryRemoveItem(player.Character!, 5, 3);
    }

    [Test]
    public async Task TryDropItem_WhenSuccessful_BroadcastsItemAddToInRangePlayersExceptDropper()
    {
        var map = TestFactories.CreateMap();
        var dropper = TestFactories.CreatePlayer(1, 5, 5);
        var inRange = TestFactories.CreatePlayer(2, 6, 6);
        var outOfRange = TestFactories.CreatePlayer(3, 5, 5 + 50);
        map.Players[dropper.SessionId] = dropper;
        map.Players[inRange.SessionId] = inRange;
        map.Players[outOfRange.SessionId] = outOfRange;

        await _sut.TryDropItem(dropper, map, itemId: 7, amount: 2, new Coords { X = 5, Y = 5 });

        await _broadcastService.Received(1).BroadcastPacket(
            Arg.Is<IEnumerable<PlayerState>>(players =>
                players.Select(p => p.SessionId).OrderBy(id => id).SequenceEqual(new[] { 2 })),
            Arg.Is<IPacket>(p =>
                p is ItemAddServerPacket &&
                ((ItemAddServerPacket)p).ItemId == 7 &&
                ((ItemAddServerPacket)p).ItemIndex == 1 &&
                ((ItemAddServerPacket)p).ItemAmount == 2 &&
                ((ItemAddServerPacket)p).Coords.X == 5 &&
                ((ItemAddServerPacket)p).Coords.Y == 5),
            Arg.Any<PlayerState?>());
    }

    [Test]
    public async Task TryDropItem_WhenNoOtherPlayersInRange_BroadcastsToEmptyRecipientList()
    {
        var map = TestFactories.CreateMap();
        var dropper = TestFactories.CreatePlayer(1, 5, 5);
        map.Players[dropper.SessionId] = dropper;

        await _sut.TryDropItem(dropper, map, itemId: 7, amount: 1, new Coords { X = 5, Y = 5 });

        await _broadcastService.Received(1).BroadcastPacket(
            Arg.Is<IEnumerable<PlayerState>>(players => !players.Any()),
            Arg.Any<IPacket>(),
            Arg.Any<PlayerState?>());
    }

    [Test]
    public async Task TryDropItem_WhenTooFarAway_ReturnsFailureAndDoesNotBroadcast()
    {
        var map = TestFactories.CreateMap();
        var player = TestFactories.CreatePlayer(1, 5, 5);
        map.Players[player.SessionId] = player;

        var result = await _sut.TryDropItem(player, map, itemId: 5, amount: 1, new Coords { X = 20, Y = 20 });

        result.Success.Should().BeFalse();
        map.Items.Should().BeEmpty();
        await _broadcastService.DidNotReceive()
            .BroadcastPacket(Arg.Any<IEnumerable<PlayerState>>(), Arg.Any<IPacket>(), Arg.Any<PlayerState?>());
    }

    [Test]
    public async Task TryDropItem_WhenPlayerDoesNotHaveItem_ReturnsFailure()
    {
        var map = TestFactories.CreateMap();
        var player = TestFactories.CreatePlayer(1, 5, 5);
        map.Players[player.SessionId] = player;
        _inventoryService.HasItem(Arg.Any<Character>(), Arg.Any<int>(), Arg.Any<int>()).Returns(false);

        var result = await _sut.TryDropItem(player, map, itemId: 5, amount: 1, new Coords { X = 5, Y = 5 });

        result.Success.Should().BeFalse();
        map.Items.Should().BeEmpty();
    }
}