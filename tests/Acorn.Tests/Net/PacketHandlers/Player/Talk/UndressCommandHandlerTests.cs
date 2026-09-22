using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using NSubstitute;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $undress force-removes every equipped item from another player, sending each
///     removal as the client expects (mirrors the player-facing unequip packets).
/// </summary>
public class UndressCommandHandlerTests
{
    private static (UndressCommandHandler Sut, IWorldQueries World, IPlayerController Controller,
        INotificationService Notifications) CreateSut()
    {
        var world = Substitute.For<IWorldQueries>();
        var controller = Substitute.For<IPlayerController>();
        var notifications = Substitute.For<INotificationService>();
        var paperdollService = Substitute.For<IPaperdollService>();
        paperdollService.ToEquipmentChange(Arg.Any<EquipmentPaperdoll>()).Returns(new EquipmentChange());
        var sut = new UndressCommandHandler(world, controller, paperdollService, notifications,
            NullLogger<UndressCommandHandler>.Instance);
        return (sut, world, controller, notifications);
    }

    [Test]
    public async Task Undress_PlayerWithTwoItems_UnequipsBothAndUpdatesUi()
    {
        // Arrange
        var (sut, world, controller, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        var (target, comms) = FakePlayer.Create("Victim", 2);
        target.CurrentMap = FakeMap.Create();
        target.Character!.Paperdoll.Weapon = 2;
        target.Character.Paperdoll.Hat = 3;
        world.FindPlayerByName("Victim").Returns(target);
        controller.UnequipItemAsync(target, 2, 0).Returns(true);
        controller.UnequipItemAsync(target, 3, 0).Returns(true);

        // Act
        await sut.HandleAsync(admin, "undress", "Victim");

        // Assert
        await controller.Received(1).UnequipItemAsync(target, 2, 0);
        await controller.Received(1).UnequipItemAsync(target, 3, 0);
        comms.Sent.Should().HaveCount(2, "one PaperdollRemove reply per unequipped item");
        await notifications.Received(1).ServerAnnouncement(target, Arg.Any<string>());
        await notifications.Received(1).SystemMessage(admin,
            Arg.Is<string>(s => s.Contains("2 item(s)")));
    }

    [Test]
    public async Task Undress_CursedItemResists_CountsOnlyRemovedItems()
    {
        // Arrange
        var (sut, world, controller, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        var (target, comms) = FakePlayer.Create("Victim", 2);
        target.CurrentMap = FakeMap.Create();
        target.Character!.Paperdoll.Armor = 4;
        world.FindPlayerByName("Victim").Returns(target);
        controller.UnequipItemAsync(target, 4, 0).Returns(false);

        // Act
        await sut.HandleAsync(admin, "undress", "Victim");

        // Assert
        comms.Sent.Should().BeEmpty();
        await notifications.DidNotReceive().ServerAnnouncement(target, Arg.Any<string>());
        await notifications.Received(1).SystemMessage(admin,
            Arg.Is<string>(s => s.StartsWith("No equipment")));
    }

    [Test]
    public async Task Undress_TargetOffline_ReportsNotOnline()
    {
        // Arrange
        var (sut, world, controller, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);

        // Act
        await sut.HandleAsync(admin, "undress", "Ghost");

        // Assert
        await controller.DidNotReceiveWithAnyArgs().UnequipItemAsync(default!, default, default);
        await notifications.Received(1).SystemMessage(admin, Arg.Is<string>(s => s.Contains("not online")));
    }
}
