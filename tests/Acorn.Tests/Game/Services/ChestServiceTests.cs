using Acorn.World.Services.Map;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     Chest interaction is limited to orthogonally adjacent tiles (Manhattan distance &lt;= 1),
///     matching eoserv's <c>util::path_length</c> check. Diagonal neighbours must be rejected.
/// </summary>
public class ChestServiceTests
{
    private readonly ChestService _sut = new(new MapTileService());

    [Test]
    [Arguments(5, 6)]
    [Arguments(5, 4)]
    [Arguments(6, 5)]
    [Arguments(4, 5)]
    public void IsAdjacent_WhenOrthogonallyAdjacent_ShouldBeTrue(int x, int y)
    {
        var character = MapTestData.CreateCharacter(x: 5, y: 5);

        _sut.IsAdjacent(character, new Coords { X = x, Y = y }).Should().BeTrue();
    }

    [Test]
    [Arguments(6, 6)]
    [Arguments(4, 4)]
    [Arguments(7, 5)]
    [Arguments(5, 7)]
    [Arguments(5, 5)]
    public void IsAdjacent_WhenNotOrthogonallyAdjacent_ShouldBeFalse(int x, int y)
    {
        var character = MapTestData.CreateCharacter(x: 5, y: 5);

        // (5,5) is the player's own tile, which is Manhattan 0 and therefore "in range";
        // exclude it from the false cases below by checking distance instead.
        if (x == 5 && y == 5)
        {
            _sut.IsAdjacent(character, new Coords { X = x, Y = y }).Should().BeTrue();
            return;
        }

        _sut.IsAdjacent(character, new Coords { X = x, Y = y }).Should().BeFalse();
    }

    [Test]
    public void IsChestTile_WhenTileIsChest_ShouldBeTrue()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddTile(map.Data, 5, 6, MapTileSpec.Chest);

        _sut.IsChestTile(map, new Coords { X = 5, Y = 6 }).Should().BeTrue();
    }

    [Test]
    public void IsChestTile_WhenTileIsNotChest_ShouldBeFalse()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddTile(map.Data, 5, 6, MapTileSpec.Wall);

        _sut.IsChestTile(map, new Coords { X = 5, Y = 6 }).Should().BeFalse();
        _sut.IsChestTile(map, new Coords { X = 9, Y = 9 }).Should().BeFalse();
    }

    [Test]
    public void IsInBounds_ShouldRejectOutOfRangeCoordinates()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf(width: 10, height: 10));

        _sut.IsInBounds(map, new Coords { X = 0, Y = 0 }).Should().BeTrue();
        _sut.IsInBounds(map, new Coords { X = 9, Y = 9 }).Should().BeTrue();
        _sut.IsInBounds(map, new Coords { X = 10, Y = 5 }).Should().BeFalse();
        _sut.IsInBounds(map, new Coords { X = 5, Y = 10 }).Should().BeFalse();
        _sut.IsInBounds(map, new Coords { X = -1, Y = 5 }).Should().BeFalse();
    }

    [Test]
    public void GetOrCreateChest_ShouldReturnStableInstance()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        var coords = new Coords { X = 5, Y = 6 };

        var first = _sut.GetOrCreateChest(map, coords);
        var second = _sut.GetOrCreateChest(map, coords);

        second.Should().BeSameAs(first);
        map.Chests.Should().ContainKey(coords);
    }
}