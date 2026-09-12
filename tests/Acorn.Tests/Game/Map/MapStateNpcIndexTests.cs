using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Game.Map;

/// <summary>
///     NPCs are addressed by a stable byte index in NpcMapInfo/NpcUpdate packets, and
///     the client sends that same index back for range requests and interactions. These
///     tests lock down that the index is the map dictionary key and never shifts when
///     other NPCs die (previously it was derived from the enumeration position, which
///     could diverge from the key and hide NPCs on the client).
/// </summary>
public class MapStateNpcIndexTests
{
    private const int NpcTypeId = 1;

    private static MapState CreateMap(int npcCount)
    {
        var enf = new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 1,
            Npcs = new List<EnfRecord>
            {
                new() { Name = "TestNpc", Hp = 100, Level = 1, Type = PubNpcType.Passive }
            }
        };

        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(enf);

        var npcController = Substitute.For<INpcController>();
        npcController.ShouldUseSpawnVariance(Arg.Any<NpcState>()).Returns(false);

        var emf = new Emf
        {
            Width = 20,
            Height = 20,
            Npcs = new List<MapNpc>
            {
                new()
                {
                    Id = NpcTypeId,
                    Amount = npcCount,
                    SpawnType = 0,
                    SpawnTime = 60,
                    Coords = new Coords { X = 5, Y = 5 }
                }
            },
            TileSpecRows = new List<MapTileSpecRow>()
        };

        return new MapState(
            new MapWithId(1, emf),
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            npcController,
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            Substitute.For<ILogger<MapState>>());
    }

    [Test]
    public void NpcIndexes_ShouldMatchDictionaryKeys()
    {
        var map = CreateMap(npcCount: 3);

        map.Npcs.Keys.OrderBy(k => k).Should().Equal(0, 1, 2);
        map.Npcs.Values.Select(n => n.Index).OrderBy(i => i).Should().Equal(0, 1, 2);
    }

    [Test]
    public void AsNpcMapInfo_ShouldReturnStableIndexes()
    {
        var map = CreateMap(npcCount: 3);

        var infos = map.AsNpcMapInfo();

        infos.Select(i => i.Index).Should().Equal(0, 1, 2);
    }

    [Test]
    public void AsNpcMapInfo_WhenAnNpcDies_ShouldKeepRemainingIndexesStable()
    {
        var map = CreateMap(npcCount: 3);

        // Kill the middle NPC.
        map.Npcs[1].IsDead = true;

        var infos = map.AsNpcMapInfo();

        infos.Select(i => i.Index).Should().Equal(new[] { 0, 2 },
            "dead NPCs are omitted but the surviving NPCs keep their original index");
    }

    [Test]
    public void GetNextNpcIndex_ShouldReturnLowestFreeIndex()
    {
        var map = CreateMap(npcCount: 3);

        map.GetNextNpcIndex().Should().Be(3);

        // Simulate a gap.
        map.Npcs.TryRemove(1, out _);
        map.GetNextNpcIndex().Should().Be(1);
    }
}