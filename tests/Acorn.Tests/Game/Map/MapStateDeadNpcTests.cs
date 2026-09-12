using Acorn.Tests.TestSupport;
using Acorn.World.Map;
using Acorn.World.Npc;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Game.Map;

/// <summary>
///     A dead NPC stays on the map until it respawns, but it must not block movement
///     (eoserv's Map::Occupied only counts living NPCs). Admin-spawned NPCs are
///     temporary, as in eoserv, and are removed from the map entirely once they die.
/// </summary>
public class MapStateDeadNpcTests
{
    private static NpcState AddNpc(MapState map, int index, int x, int y, bool isDead = false,
        bool isAdminSpawned = false)
    {
        var npc = new NpcState(new EnfRecord { Name = $"Npc{index}", Hp = 10, Level = 1, Type = PubNpcType.Passive })
        {
            Index = index,
            Id = 1,
            X = x,
            Y = y,
            Hp = 10,
            IsDead = isDead,
            IsAdminSpawned = isAdminSpawned
        };

        map.Npcs[index] = npc;
        return npc;
    }

    [Test]
    public void IsTileOccupied_WhenLivingNpcIsOnTile_ShouldReturnTrue()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, x: 5, y: 5);

        map.IsTileOccupied(new Coords { X = 5, Y = 5 }).Should().BeTrue();
    }

    [Test]
    public void IsTileOccupied_WhenOnlyADeadNpcIsOnTile_ShouldReturnFalse()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, x: 5, y: 5, isDead: true);

        map.IsTileOccupied(new Coords { X = 5, Y = 5 })
            .Should().BeFalse("dead NPCs do not block movement");
    }

    [Test]
    public void IsTileOccupied_WhenDeadAndLivingNpcShareATile_ShouldReturnTrue()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, x: 5, y: 5, isDead: true);
        AddNpc(map, index: 1, x: 5, y: 5);

        map.IsTileOccupied(new Coords { X = 5, Y = 5 })
            .Should().BeTrue("a living NPC on the same tile still blocks it");
    }

    [Test]
    public void RemoveNpc_ShouldRemoveNpcFromTheMap()
    {
        var map = FakeMap.Create();
        var npc = AddNpc(map, index: 0, x: 5, y: 5, isDead: true, isAdminSpawned: true);

        var removed = map.RemoveNpc(npc);

        removed.Should().BeTrue();
        map.Npcs.Should().NotContainKey(0);
    }

    [Test]
    public void RemoveNpc_WhenNpcIsAlreadyGone_ShouldReturnFalse()
    {
        var map = FakeMap.Create();
        var npc = AddNpc(map, index: 0, x: 5, y: 5);
        map.RemoveNpc(npc);

        map.RemoveNpc(npc).Should().BeFalse();
    }
}