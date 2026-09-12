using Acorn.World.Services.Map;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Tile walkability: NPC boundary tiles constrain NPC movement only. Players must be
///     able to walk over them, matching eoserv's <c>Map_Tile::Walkable</c> which returns
///     <c>!npc</c> for an NPC boundary. This keeps town exits and doorway approaches
///     marked as NPC boundaries usable for players.
/// </summary>
public class MapTileServiceTests
{
    private static Emf CreateMapWithTile(int x, int y, MapTileSpec spec)
    {
        return new Emf
        {
            Width = 20,
            Height = 20,
            TileSpecRows = new List<MapTileSpecRow>
            {
                new()
                {
                    Y = y,
                    Tiles = new List<MapTileSpecRowTile>
                    {
                        new() { X = x, TileSpec = spec }
                    }
                }
            }
        };
    }

    [Test]
    public void IsTileWalkable_WhenTileIsNpcBoundary_ShouldReturnTrue()
    {
        var map = CreateMapWithTile(5, 5, MapTileSpec.NpcBoundary);

        new MapTileService().IsTileWalkable(map, new Coords { X = 5, Y = 5 })
            .Should().BeTrue("NPC boundary tiles only block NPCs");
    }

    [Test]
    public void IsNpcWalkable_WhenTileIsNpcBoundary_ShouldReturnFalse()
    {
        new MapTileService().IsNpcWalkable(MapTileSpec.NpcBoundary)
            .Should().BeFalse("NPCs must not walk over an NPC boundary");
    }

    [Test]
    [Arguments(MapTileSpec.Wall)]
    [Arguments(MapTileSpec.Chest)]
    [Arguments(MapTileSpec.Edge)]
    [Arguments(MapTileSpec.BankVault)]
    public void IsTileWalkable_WhenTileBlocksEveryone_ShouldReturnFalse(MapTileSpec spec)
    {
        var map = CreateMapWithTile(5, 5, spec);

        new MapTileService().IsTileWalkable(map, new Coords { X = 5, Y = 5 }).Should().BeFalse();
    }

    [Test]
    [Arguments(MapTileSpec.Wall)]
    [Arguments(MapTileSpec.Chest)]
    [Arguments(MapTileSpec.Edge)]
    [Arguments(MapTileSpec.BankVault)]
    public void IsNpcWalkable_WhenTileBlocksEveryone_ShouldReturnFalse(MapTileSpec spec)
    {
        new MapTileService().IsNpcWalkable(spec).Should().BeFalse();
    }

    [Test]
    public void IsTileWalkable_WhenNoTileSpec_ShouldReturnTrue()
    {
        var map = new Emf
        {
            Width = 20,
            Height = 20,
            TileSpecRows = new List<MapTileSpecRow>()
        };

        new MapTileService().IsTileWalkable(map, new Coords { X = 5, Y = 5 }).Should().BeTrue();
    }
}