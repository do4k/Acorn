using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Xunit;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Game.Services;

public class NpcControllerTests
{
    private readonly NpcController _sut = new(new MapTileService());

    private static NpcState CreateNpc(PubNpcType type = PubNpcType.Passive)
    {
        var enf = new EnfRecord { Name = "TestNpc", Type = type, Hp = 100, Level = 1 };
        return new NpcState(enf) { SpawnType = 0 };
    }

    private static Emf CreateMap(int width = 20, int height = 20)
    {
        return new Emf
        {
            Width = width,
            Height = height,
            Npcs = new List<MapNpc>(),
            TileSpecRows = new List<MapTileSpecRow>()
        };
    }

    private static void AddTile(Emf map, int x, int y, MapTileSpec spec)
    {
        map.TileSpecRows.Add(new MapTileSpecRow
        {
            Y = y,
            Tiles = new List<MapTileSpecRowTile> { new() { X = x, TileSpec = spec } }
        });
    }

    [Fact]
    public void FindSpawnPosition_WhenBaseNearEdge_ShouldStayWithinMapBounds()
    {
        // Arrange - tiles are 0-indexed, so the last valid coordinate is Width-1 / Height-1
        var map = CreateMap(width: 5, height: 5);
        var npc = CreateNpc();

        // Act
        for (var i = 0; i < 200; i++)
        {
            var (x, y) = _sut.FindSpawnPosition(npc, baseX: 4, baseY: 4,
                players: Enumerable.Empty<PlayerState>(), npcs: Enumerable.Empty<NpcState>(), map);

            // Assert - never spawn on a tile index equal to Width/Height
            x.Should().BeInRange(0, map.Width - 1);
            y.Should().BeInRange(0, map.Height - 1);
        }
    }

    [Fact]
    public void FindSpawnPosition_WhenBaseIsWall_ShouldSpawnOnWalkableTile()
    {
        // Arrange
        var map = CreateMap();
        AddTile(map, x: 10, y: 10, MapTileSpec.Wall);
        var npc = CreateNpc();

        // Act
        var (x, y) = _sut.FindSpawnPosition(npc, baseX: 10, baseY: 10,
            players: Enumerable.Empty<PlayerState>(), npcs: Enumerable.Empty<NpcState>(), map);

        // Assert - never place the NPC on a wall tile
        (x, y).Should().NotBe((10, 10));
    }

    [Fact]
    public void FindSpawnPosition_WhenBaseIsNpcBoundary_ShouldStayInsideBoundary()
    {
        // Arrange
        var map = CreateMap();
        AddTile(map, x: 10, y: 10, MapTileSpec.NpcBoundary);
        var npc = CreateNpc();

        // Act
        var (x, y) = _sut.FindSpawnPosition(npc, baseX: 10, baseY: 10,
            players: Enumerable.Empty<PlayerState>(), npcs: Enumerable.Empty<NpcState>(), map);

        // Assert - never place the NPC on an NPC boundary (so it can't break out of bounds)
        (x, y).Should().NotBe((10, 10));
    }

    [Fact]
    public void FindSpawnPosition_WhenTileOccupiedByOtherNpc_ShouldAvoidStacking()
    {
        // Arrange
        var map = CreateMap();
        var occupant = CreateNpc();
        occupant.X = 10;
        occupant.Y = 10;
        var npc = CreateNpc();

        // Act
        var (x, y) = _sut.FindSpawnPosition(npc, baseX: 10, baseY: 10,
            players: Enumerable.Empty<PlayerState>(), npcs: new[] { occupant }, map);

        // Assert - avoid spawning on top of another living NPC
        (x, y).Should().NotBe((10, 10));
    }
}
