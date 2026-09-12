using Acorn.Database.Repository;
using Acorn.World.Services.Map;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     Door open validation: coordinates must be in bounds, point at a door warp,
///     be within interaction range, and satisfy the key requirement for locked doors.
/// </summary>
public class DoorServiceTests
{
    private static DoorService CreateSut(Eif? eif = null)
    {
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(eif ?? new Eif
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalItemsCount = 0,
            Items = new List<EifRecord>()
        });

        return new DoorService(new MapTileService(), dataFileRepository,
            Substitute.For<ILogger<DoorService>>());
    }

    [Test]
    public void ValidateDoorOpen_WhenValidDoorInRange_ShouldReturnOpened()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 1);
        var character = MapTestData.CreateCharacter(x: 5, y: 6);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.Opened);
    }

    [Test]
    public void ValidateDoorOpen_WhenNoWarpAtCoords_ShouldReturnNotADoor()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        var character = MapTestData.CreateCharacter(x: 5, y: 6);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.NotADoor);
    }

    [Test]
    public void ValidateDoorOpen_WhenWarpIsNotADoor_ShouldReturnNotADoor()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 0);
        var character = MapTestData.CreateCharacter(x: 5, y: 6);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.NotADoor);
    }

    [Test]
    public void ValidateDoorOpen_WhenOutOfBounds_ShouldReturnNotADoor()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf(width: 10, height: 10));
        var character = MapTestData.CreateCharacter(x: 5, y: 5);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = -1, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.NotADoor);
    }

    [Test]
    public void ValidateDoorOpen_WhenOutOfRange_ShouldReturnOutOfRange()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf(width: 40, height: 40));
        MapTestData.AddWarp(map.Data, 5, 5, door: 1);
        var character = MapTestData.CreateCharacter(x: 30, y: 30);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.OutOfRange);
    }

    [Test]
    public void ValidateDoorOpen_WhenAlreadyOpen_ShouldReturnAlreadyOpen()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 1);
        map.RegisterOpenedDoor(new Coords { X = 5, Y = 5 });
        var character = MapTestData.CreateCharacter(x: 5, y: 6);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.AlreadyOpen);
    }

    [Test]
    public void ValidateDoorOpen_WhenLockedWithoutKey_ShouldReturnLockedAndRequiredKey()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 2);
        var character = MapTestData.CreateCharacter(x: 5, y: 6);

        var result = CreateSut().ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out var requiredKey);

        result.Should().Be(DoorOpenResult.Locked);
        requiredKey.Should().Be(2);
    }

    [Test]
    public void ValidateDoorOpen_WhenLockedWithMatchingKey_ShouldReturnOpened()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 2);
        var character = MapTestData.CreateCharacter(x: 5, y: 6);
        character.Inventory.Items.Add(new Acorn.Game.Models.ItemWithAmount { Id = 1, Amount = 1 });

        var result = CreateSut(MapTestData.CreateEifWithKey(keySpec: 2))
            .ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.Opened);
    }

    [Test]
    public void ValidateDoorOpen_WhenLockedWithWrongKey_ShouldReturnLocked()
    {
        var map = MapTestData.CreateMap(MapTestData.CreateEmf());
        MapTestData.AddWarp(map.Data, 5, 5, door: 2);
        var character = MapTestData.CreateCharacter(x: 5, y: 6);
        character.Inventory.Items.Add(new Acorn.Game.Models.ItemWithAmount { Id = 1, Amount = 1 });

        var result = CreateSut(MapTestData.CreateEifWithKey(keySpec: 3))
            .ValidateDoorOpen(character, new Coords { X = 5, Y = 5 }, map, out _);

        result.Should().Be(DoorOpenResult.Locked);
    }
}