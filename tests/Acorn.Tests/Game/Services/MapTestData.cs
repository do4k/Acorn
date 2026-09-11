using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     Builders for lightweight map/character state used by interaction unit tests.
/// </summary>
internal static class MapTestData
{
    public static Character CreateCharacter(int x = 5, int y = 5)
    {
        return new Character
        {
            Accounts_Username = "tester",
            Name = "Tester",
            X = x,
            Y = y,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    public static Emf CreateEmf(int width = 20, int height = 20)
    {
        return new Emf
        {
            Name = "TestMap",
            Width = width,
            Height = height,
            FillTile = 1,
            MapAvailable = true,
            CanScroll = true,
            Type = MapType.Normal,
            TimedEffect = MapTimedEffect.None,
            Npcs = new List<MapNpc>(),
            Items = new List<Moffat.EndlessOnline.SDK.Protocol.Map.MapItem>(),
            TileSpecRows = new List<MapTileSpecRow>(),
            WarpRows = new List<MapWarpRow>(),
            GraphicLayers = Enumerable.Range(0, 9)
                .Select(_ => new MapGraphicLayer { GraphicRows = new List<MapGraphicRow>() })
                .ToList(),
            Signs = new List<MapSign>(),
            LegacyDoorKeys = new List<MapLegacyDoorKey>(),
            Rid = new List<int> { 1, 2 }
        };
    }

    public static void AddTile(Emf map, int x, int y, MapTileSpec spec)
    {
        map.TileSpecRows.Add(new MapTileSpecRow
        {
            Y = y,
            Tiles = new List<MapTileSpecRowTile> { new() { X = x, TileSpec = spec } }
        });
    }

    public static void AddWarp(Emf map, int x, int y, int door)
    {
        map.WarpRows.Add(new MapWarpRow
        {
            Y = y,
            Tiles = new List<MapWarpRowTile>
            {
                new()
                {
                    X = x,
                    Warp = new MapWarp
                    {
                        DestinationMap = 0,
                        DestinationCoords = new Coords { X = 0, Y = 0 },
                        LevelRequired = 0,
                        Door = door
                    }
                }
            }
        });
    }

    public static Eif CreateEifWithKey(int keySpec)
    {
        return new Eif
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalItemsCount = 1,
            Items = new List<EifRecord>
            {
                new() { Name = "Key", Type = ItemType.Key, Spec1 = keySpec }
            }
        };
    }

    public static MapState CreateMap(Emf data)
    {
        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 0,
            Npcs = new List<EnfRecord>()
        });
        dataRepository.Eif.Returns(new Eif
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalItemsCount = 0,
            Items = new List<EifRecord>()
        });

        var npcController = Substitute.For<INpcController>();
        npcController.ShouldUseSpawnVariance(Arg.Any<NpcState>()).Returns(false);

        return new MapState(
            new MapWithId(1, data),
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            npcController,
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            Substitute.For<ILogger<MapState>>());
    }
}
