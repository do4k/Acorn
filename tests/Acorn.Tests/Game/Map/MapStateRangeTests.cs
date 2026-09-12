using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Options;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Game.Map;

/// <summary>
///     Range/visibility behaviour: NearbyInfo must only contain entities within the
///     client view range of the observer, and a warp effect must only ever be attached
///     to the character that is actually warping in.
/// </summary>
public class MapStateRangeTests
{
    private const int ClientRange = 13;

    [Fact]
    public void AsNearbyInfo_ShouldOnlyIncludeEntitiesWithinClientRange()
    {
        var map = CreateMap();
        var observer = AddPlayer(map, sessionId: 1, x: 5, y: 5, name: "Observer");
        AddPlayer(map, sessionId: 2, x: 5, y: 18, name: "InRange");   // distance 13
        AddPlayer(map, sessionId: 3, x: 5, y: 19, name: "OutOfRange"); // distance 14
        AddNpc(map, index: 0, x: 5, y: 10, id: 1);
        AddNpc(map, index: 1, x: 50, y: 50, id: 1);
        AddItem(map, uid: 1, x: 6, y: 6, id: 1);
        AddItem(map, uid: 2, x: 40, y: 40, id: 1);

        var nearby = map.AsNearbyInfo(observer);

        nearby.Characters.Select(c => c.PlayerId).Should().BeEquivalentTo(new[] { 1, 2 });
        nearby.Npcs.Select(n => n.Index).Should().Equal(0);
        nearby.Items.Select(i => i.Uid).Should().Equal(1);
    }

    [Fact]
    public void AsNearbyInfo_ShouldIncludeTheObserversOwnCharacter()
    {
        var map = CreateMap();
        var observer = AddPlayer(map, sessionId: 7, x: 5, y: 5, name: "Self");

        var nearby = map.AsNearbyInfo(observer);

        nearby.Characters.Should().ContainSingle(c => c.PlayerId == 7);
    }

    [Fact]
    public void AsNearbyInfo_ShouldNotAttachWarpEffectToAnyCharacter()
    {
        var map = CreateMap();
        var observer = AddPlayer(map, sessionId: 1, x: 5, y: 5, name: "Observer");
        AddPlayer(map, sessionId: 2, x: 6, y: 6, name: "Nearby");

        var nearby = map.AsNearbyInfo(observer);

        nearby.Characters.Should().OnlyContain(c => c.WarpEffect == WarpEffect.None,
            "a warp effect belongs to the warping character only, never to the observer's whole view");
    }

    [Fact]
    public void AsCharacterInfo_ShouldApplyWarpEffectOnlyToThatCharacter()
    {
        var map = CreateMap();
        var entering = AddPlayer(map, sessionId: 1, x: 5, y: 5, name: "Entering");
        AddPlayer(map, sessionId: 2, x: 6, y: 6, name: "Other");

        var info = map.AsCharacterInfo(entering, WarpEffect.Admin);

        info.Characters.Should().ContainSingle();
        info.Characters[0].PlayerId.Should().Be(1);
        info.Characters[0].WarpEffect.Should().Be(WarpEffect.Admin);
    }

    [Fact]
    public void AsNearbyInfo_WithRequestedIds_ShouldOnlyReturnRequestedEntities()
    {
        var map = CreateMap();
        var observer = AddPlayer(map, sessionId: 1, x: 5, y: 5, name: "Observer");
        AddPlayer(map, sessionId: 2, x: 6, y: 6, name: "Requested");
        AddPlayer(map, sessionId: 3, x: 7, y: 7, name: "NotRequested");
        AddNpc(map, index: 0, x: 5, y: 6, id: 1);
        AddNpc(map, index: 1, x: 5, y: 7, id: 1);

        var nearby = map.AsNearbyInfo(observer, playerIds: new[] { 2 }, npcIndexes: new[] { 1 });

        nearby.Characters.Select(c => c.PlayerId).Should().Equal(2);
        nearby.Npcs.Select(n => n.Index).Should().Equal(1);
        nearby.Items.Should().BeEmpty("a range request does not ask for ground items");
    }

    [Fact]
    public void AsNearbyInfo_WithRequestedIds_ShouldStillRangeFilter()
    {
        var map = CreateMap();
        var observer = AddPlayer(map, sessionId: 1, x: 5, y: 5, name: "Observer");
        AddPlayer(map, sessionId: 2, x: 60, y: 60, name: "FarAway");
        AddNpc(map, index: 0, x: 60, y: 60, id: 1);

        var nearby = map.AsNearbyInfo(observer, playerIds: new[] { 2 }, npcIndexes: new[] { 0 });

        nearby.Characters.Should().BeEmpty();
        nearby.Npcs.Should().BeEmpty();
    }

    // --- helpers ---

    private static MapState CreateMap()
    {
        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 0,
            Npcs = new List<EnfRecord>()
        });

        var npcController = Substitute.For<INpcController>();
        npcController.ShouldUseSpawnVariance(Arg.Any<NpcState>()).Returns(false);

        var tileService = Substitute.For<IMapTileService>();
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>())
            .Returns(call =>
            {
                var a = call.ArgAt<Coords>(0);
                var b = call.ArgAt<Coords>(1);
                return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= ClientRange;
            });

        var emf = new Emf
        {
            Width = 100,
            Height = 100,
            Npcs = new List<MapNpc>(),
            TileSpecRows = new List<MapTileSpecRow>()
        };

        return new MapState(
            new MapWithId(1, emf),
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            npcController,
            tileService,
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            Substitute.For<ILogger<MapState>>());
    }

    private static PlayerState AddPlayer(MapState map, int sessionId, int x, int y, string name)
    {
        var player = CreatePlayer(sessionId, x, y, name);
        map.Players[sessionId] = player;
        return player;
    }

    private static PlayerState CreatePlayer(int sessionId, int x, int y, string name)
    {
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);

        var player = new PlayerState(
            Array.Empty<Acorn.Net.PacketHandlers.IPacketHandler>(),
            communicator,
            Substitute.For<ILogger<PlayerState>>(),
            Microsoft.Extensions.Options.Options.Create(new ServerOptions
            {
                NewCharacter = new NewCharacterOptions { X = 1, Y = 1, Map = 1 },
                Hosting = new HostingOptions
                {
                    HostName = "localhost",
                    Port = 1,
                    WebSocketPort = 2,
                    SLN = new SLNOptions
                    {
                        Enabled = false,
                        Url = "http://localhost",
                        PingRate = 5,
                        UserAgent = "test",
                        Zone = "test",
                        ServerName = "test",
                        Site = "http://localhost"
                    }
                },
                TickRate = 1000
            }),
            new AcornMetrics(),
            sessionId,
            _ => Task.CompletedTask)
        {
            Character = new Character
            {
                Accounts_Username = "test",
                Name = name,
                Map = 1,
                X = x,
                Y = y,
                Class = 1,
                Level = 1,
                MaxHp = 10,
                Hp = 10,
                MaxTp = 10,
                Tp = 10,
                Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
                Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
                Paperdoll = new Paperdoll(),
                Spells = new Spells(new ConcurrentBag<Spell>())
            }
        };

        return player;
    }

    private static void AddNpc(MapState map, int index, int x, int y, int id)
    {
        var npc = new NpcState(new EnfRecord { Name = $"Npc{index}", Hp = 10, Level = 1, Type = PubNpcType.Passive })
        {
            Index = index,
            Id = id,
            X = x,
            Y = y,
            Direction = Direction.Down
        };

        map.Npcs[index] = npc;
    }

    private static void AddItem(MapState map, int uid, int x, int y, int id)
    {
        map.Items[uid] = new Acorn.World.Map.MapItem
        {
            Id = id,
            Amount = 1,
            Coords = new Coords { X = x, Y = y }
        };
    }
}
