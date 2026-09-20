using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Shared.Caching;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

/// <summary>
///     Regression tests for $set. The sitstate/hidden/nointeract attributes used to
///     read a non-existent argument (args[3]) and were gated behind an integer parse,
///     so they could never be applied.
/// </summary>
public class SetCommandHandlerTests
{
    private static (SetCommandHandler Sut, INotificationService Notifications, PlayerState Target) CreateSut()
    {
        var world = Substitute.For<IWorldQueries>();
        var notifications = Substitute.For<INotificationService>();
        var playerController = Substitute.For<IPlayerController>();
        var characterCache = Substitute.For<ICharacterCacheService>();
        var paperdollService = Substitute.For<IPaperdollService>();

        var (target, _) = FakePlayer.Create("Target", 2);
        world.GetAllPlayers().Returns(new[] { target });

        var sut = new SetCommandHandler(world, NullLogger<SetCommandHandler>.Instance, notifications,
            playerController, characterCache, paperdollService);

        return (sut, notifications, target);
    }

    private static PlayerState Admin()
    {
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.GameMaster;
        return admin;
    }

    [Test]
    public async Task SetHidden_AppliesBooleanValue()
    {
        var (sut, _, target) = CreateSut();

        await sut.HandleAsync(Admin(), "set", "Target", "hidden", "true");

        target.Character!.Hidden.Should().BeTrue();
    }

    [Test]
    public async Task SetNoInteract_AcceptsNumericBoolean()
    {
        var (sut, _, target) = CreateSut();

        await sut.HandleAsync(Admin(), "set", "Target", "nointeract", "1");

        target.Character!.NoInteract.Should().BeTrue();
    }

    [Test]
    public async Task SetSitState_ParsesEnumName()
    {
        var (sut, _, target) = CreateSut();

        await sut.HandleAsync(Admin(), "set", "Target", "sitstate", "Stand");

        target.Character!.SitState.Should().Be(SitState.Stand);
    }

    [Test]
    public async Task SetIntegerAttribute_AppliesValue()
    {
        var (sut, _, target) = CreateSut();

        await sut.HandleAsync(Admin(), "set", "Target", "level", "42");

        target.Character!.Level.Should().Be(42);
    }

    [Test]
    public async Task SetIntegerAttribute_WithNonNumeric_ReportsErrorAndLeavesValue()
    {
        var (sut, notifications, target) = CreateSut();
        var originalLevel = target.Character!.Level;

        await sut.HandleAsync(Admin(), "set", "Target", "level", "abc");

        target.Character.Level.Should().Be(originalLevel);
        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("must be an integer")));
    }

    [Test]
    public async Task SetAdmin_WhenCallerBelowHighGameMaster_IsRejected()
    {
        var (sut, notifications, target) = CreateSut();
        var caller = Admin();
        caller.Character!.Admin = AdminLevel.GameMaster;
        var originalAdmin = target.Character!.Admin;

        await sut.HandleAsync(caller, "set", "Target", "admin", "5");

        target.Character.Admin.Should().Be(originalAdmin);
        await notifications.Received(1)
            .SystemMessage(caller, Arg.Is<string>(m => m.Contains("HighGameMaster")));
    }

    [Test]
    public async Task SetAdmin_WhenCallerIsHighGameMaster_IsApplied()
    {
        var (sut, _, target) = CreateSut();
        var caller = Admin();
        caller.Character!.Admin = AdminLevel.HighGameMaster;

        await sut.HandleAsync(caller, "set", "Target", "admin", "5");

        target.Character!.Admin.Should().Be((AdminLevel)5);
    }
}
