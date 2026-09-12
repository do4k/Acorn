using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Spawn placement: multiple NPCs sharing an EMF spawn point must be spread onto
///     distinct nearby tiles. eoserv gives every NPC with spawn type below 7 random
///     variance - not just aggressive/passive combat NPCs - so quest and friendly NPCs
///     (which often share a spawn point in EMF data) do not all stack on one tile.
/// </summary>
public class NpcSpawnPlacementTests
{
    private static NpcState CreateNpc(int spawnType, PubNpcType type)
    {
        return new NpcState(new EnfRecord { Name = "TestNpc", Hp = 10, Level = 1, Type = type })
        {
            SpawnType = spawnType
        };
    }

    [Test]
    [Arguments(PubNpcType.Passive)]
    [Arguments(PubNpcType.Aggressive)]
    [Arguments(PubNpcType.Quest)]
    [Arguments(PubNpcType.Friendly)]
    public void ShouldUseSpawnVariance_WhenSpawnTypeIsNotFixed_ShouldReturnTrue(PubNpcType type)
    {
        var controller = new NpcController(new MapTileService());

        controller.ShouldUseSpawnVariance(CreateNpc(spawnType: 5, type)).Should().BeTrue(
            "every NPC with spawn type below 7 gets random variance in eoserv, not just combat NPCs");
    }

    [Test]
    public void ShouldUseSpawnVariance_WhenSpawnTypeIsSeven_ShouldReturnFalse()
    {
        var controller = new NpcController(new MapTileService());

        controller.ShouldUseSpawnVariance(CreateNpc(spawnType: 7, PubNpcType.Quest)).Should().BeFalse(
            "spawn type 7 keeps its exact EMF position and direction");
    }

    [Test]
    public void MapState_WhenNonCombatNpcsShareASpawnPoint_ShouldPlaceThemOnDistinctTiles()
    {
        // Regression: map 5 has Dan, Jacob, Paul, Candy and Hactor all at (41,47).
        const int npcCount = 5;
        var map = CreateMap(npcCount);

        var positions = map.Npcs.Values.Select(npc => (npc.X, npc.Y)).ToList();

        positions.Should().OnlyContain(p => p.X >= 8 && p.X <= 12 && p.Y >= 8 && p.Y <= 12,
            "variance stays within the ±2 tile spawn area");
        positions.Should().OnlyHaveUniqueItems("NPCs must never be placed on the same tile");
    }

    private static MapState CreateMap(int npcCount)
    {
        var enf = new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 1,
            Npcs = new List<EnfRecord>
            {
                new() { Name = "QuestNpc", Hp = 10, Level = 1, Type = PubNpcType.Quest }
            }
        };

        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(enf);

        var emf = new Emf
        {
            Width = 20,
            Height = 20,
            Npcs = new List<MapNpc>
            {
                new()
                {
                    Id = 1,
                    Amount = npcCount,
                    SpawnType = 5,
                    SpawnTime = 2,
                    Coords = new Coords { X = 10, Y = 10 }
                }
            },
            TileSpecRows = new List<MapTileSpecRow>()
        };

        return new MapState(
            new MapWithId(1, emf),
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            new NpcController(new MapTileService()),
            new MapTileService(),
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            NullLogger<MapState>.Instance);
    }
}