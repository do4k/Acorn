using Acorn.Data;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Citizen;
using Acorn.Tests.Game.Services;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Net.PacketHandlers.Citizen;

/// <summary>
///     Accepting inn sleep must restore HP/TP, take the gold and warp the player to
///     the inn's configured sleeping area.
/// </summary>
public class CitizenAcceptSleepWarpTests
{
    private const int InnBehaviorId = 3;
    private const int SleepMapId = 9;
    private const int GoldItemId = 1;

    private static (CitizenAcceptClientPacketHandler Sut, PlayerState Player, CapturingCommunicator Comm,
        IPlayerController Controller, IWorldQueries World) CreateSut(int sleepMapId = SleepMapId, int sleepX = 3,
        int sleepY = 4)
    {
        var (player, comm) = FakePlayer.Create("Sleeper", 1);
        player.Character!.Home = "Aeven";
        player.Character.X = 5;
        player.Character.Y = 5;
        player.Character.Hp = 1;
        player.Character.MaxHp = 10;
        player.Character.Tp = 2;
        player.Character.MaxTp = 10;
        player.SleepCost = 10;
        player.InteractingNpcIndex = 0;

        var map = FakeMap.Create();
        map.Npcs[0] = new NpcState(new EnfRecord
        {
            Name = "Citizen",
            Type = PubNpcType.Inn,
            BehaviorId = InnBehaviorId
        })
        {
            Index = 0,
            X = 5,
            Y = 4
        };
        player.CurrentMap = map;

        var inns = Substitute.For<IInnDataRepository>();
        inns.GetInnByBehaviorId(InnBehaviorId).Returns(new InnData(
            InnBehaviorId, "Aeven", 1, 0, 0, sleepMapId, sleepX, sleepY, false, 0, 0, 0, []));

        var inventory = Substitute.For<IInventoryService>();
        inventory.GetItemAmount(player.Character, GoldItemId).Returns(50, 10);
        inventory.TryRemoveItem(player.Character, GoldItemId, 10).Returns(true);

        var world = Substitute.For<IWorldQueries>();
        world.FindMap(SleepMapId).Returns(FakeMap.Create());

        var controller = Substitute.For<IPlayerController>();

        var sut = new CitizenAcceptClientPacketHandler(
            NullLogger<CitizenAcceptClientPacketHandler>.Instance,
            inns, inventory, world, controller);

        return (sut, player, comm, controller, world);
    }

    [Test]
    public async Task Sleep_WithSleepLocationConfigured_WarpsPlayerToBedArea()
    {
        // Arrange
        var (sut, player, _, controller, world) = CreateSut();
        var sleepMap = world.FindMap(SleepMapId);

        // Act
        await sut.HandleAsync(player, new CitizenAcceptClientPacket());

        // Assert
        await controller.Received(1).WarpAsync(player, sleepMap!, 3, 4, WarpEffect.Scroll);
    }

    [Test]
    public async Task Sleep_RestoresHpTpAndChargesGold()
    {
        // Arrange
        var (sut, player, comm, _, _) = CreateSut();

        // Act
        await sut.HandleAsync(player, new CitizenAcceptClientPacket());

        // Assert
        player.Character!.Hp.Should().Be(player.Character.MaxHp);
        player.Character.Tp.Should().Be(player.Character.MaxTp);
        player.SleepCost.Should().BeNull();
        comm.Sent.Should().ContainSingle("the accept reply is sent once");
    }

    [Test]
    public async Task Sleep_WithNoSleepLocationConfigured_DoesNotWarp()
    {
        // Arrange
        var (sut, player, _, controller, _) = CreateSut(sleepMapId: 0);

        // Act
        await sut.HandleAsync(player, new CitizenAcceptClientPacket());

        // Assert
        await controller.DidNotReceiveWithAnyArgs()
            .WarpAsync(default!, default!, default, default, default);
    }

    [Test]
    public async Task Sleep_WhenSleepMapMissingFromWorld_StillCompletesSleepWithoutWarp()
    {
        // Arrange
        var (sut, player, _, controller, world) = CreateSut();
        world.FindMap(Arg.Any<int>()).Returns((MapState?)null);

        // Act
        await sut.HandleAsync(player, new CitizenAcceptClientPacket());

        // Assert
        await controller.DidNotReceiveWithAnyArgs()
            .WarpAsync(default!, default!, default, default, default);
        player.Character!.Hp.Should().Be(player.Character.MaxHp,
            "sleep still succeeds when the configured sleep map is unavailable");
    }
}
