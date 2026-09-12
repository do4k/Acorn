using Acorn.Game.Services;
using Acorn.Shared.Caching;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;
using System.Threading.Tasks;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Dead-NPC lifecycle: NPCs that respawn are kept on the map until their respawn
///     timer elapses, while admin-spawned (temporary) NPCs are removed once they die,
///     matching eoserv's handling of temporary NPCs.
/// </summary>
public class MapControllerNpcCleanupTests
{
    private static MapController CreateController()
    {
        return new MapController(
            Substitute.For<IMapTileService>(),
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<INpcCombatService>(),
            Substitute.For<INpcController>(),
            Substitute.For<IPlayerController>(),
            Substitute.For<IFormulaService>(),
            NullLogger<MapController>.Instance,
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            new Lazy<WorldState>(() => null!));
    }

    private static void AddNpc(MapState map, int index, bool isDead, bool isAdminSpawned)
    {
        var npc = new NpcState(new EnfRecord { Name = $"Npc{index}", Hp = 10, Level = 1, Type = PubNpcType.Passive })
        {
            Index = index,
            Id = 1,
            X = 5,
            Y = 5,
            Hp = 10,
            IsDead = isDead,
            DeathTime = isDead ? DateTime.UtcNow : null,
            IsAdminSpawned = isAdminSpawned
        };

        map.Npcs[index] = npc;
    }

    [Test]
    public async Task ProcessNpcRespawnsAsync_WhenAdminSpawnedNpcIsDead_ShouldRemoveIt()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, isDead: true, isAdminSpawned: true);

        await CreateController().ProcessNpcRespawnsAsync(map);

        map.Npcs.Should().NotContainKey(0, "admin-spawned NPCs never respawn");
    }

    [Test]
    public async Task ProcessNpcRespawnsAsync_WhenAdminSpawnedNpcIsAlive_ShouldKeepIt()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, isDead: false, isAdminSpawned: true);

        await CreateController().ProcessNpcRespawnsAsync(map);

        map.Npcs.Should().ContainKey(0);
    }

    [Test]
    public async Task ProcessNpcRespawnsAsync_WhenRegularNpcIsDead_ShouldKeepItForRespawn()
    {
        var map = FakeMap.Create();
        AddNpc(map, index: 0, isDead: true, isAdminSpawned: false);

        await CreateController().ProcessNpcRespawnsAsync(map);

        map.Npcs.Should().ContainKey(0);
    }
}