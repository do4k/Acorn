using Acorn.Net;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Admin;
using Acorn.World.Services.Bans;
using Acorn.World.Services.Player;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Tests for the $wmt/$warpmeto (warp me to player) and $summon/$bring/$warptome
///     (warp player to me) admin commands.
/// </summary>
public class AdminServiceWarpTests
{
    private static (AdminService Sut, IWorldQueries World, IPlayerController PlayerController,
        INotificationService Notifications) CreateSut()
    {
        var world = Substitute.For<IWorldQueries>();
        var playerController = Substitute.For<IPlayerController>();
        var notifications = Substitute.For<INotificationService>();
        var banService = Substitute.For<IBanService>();
        var options = Microsoft.Extensions.Options.Options.Create(FakePlayer.CreateOptions());
        var logger = NullLogger<AdminService>.Instance;
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var sut = new AdminService(world, playerController, notifications, banService, options, scopeFactory, logger);

        return (sut, world, playerController, notifications);
    }

    [Test]
    public async Task WarpToPlayer_WhenTargetOnline_WarpsAdminToTargetLocation()
    {
        var (sut, world, playerController, _) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.LightGuide;

        var (target, _) = FakePlayer.Create("Target", 2);
        var targetMap = FakeMap.Create();
        target.CurrentMap = targetMap;
        target.Character!.X = 10;
        target.Character.Y = 11;
        world.FindPlayerByName("Target").Returns(target);

        await sut.WarpToPlayerAsync(admin, "Target");

        await playerController.Received(1).WarpAsync(admin, targetMap, 10, 11, WarpEffect.Admin);
    }

    [Test]
    public async Task WarpToPlayer_WhenTargetOffline_DoesNotWarp()
    {
        var (sut, world, playerController, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.LightGuide;
        world.FindPlayerByName("Ghost").Returns((PlayerState?)null);

        await sut.WarpToPlayerAsync(admin, "Ghost");

        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
        await notifications.Received(1).SystemMessage(admin, "Player 'Ghost' is not online.");
    }

    [Test]
    public async Task WarpToPlayer_WhenAdminBelowLightGuide_DoesNothing()
    {
        var (sut, world, playerController, _) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.Spy;

        await sut.WarpToPlayerAsync(admin, "Target");

        world.DidNotReceive().FindPlayerByName(Arg.Any<string>());
        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
    }

    [Test]
    public async Task SummonPlayer_WhenTargetOnline_WarpsTargetToAdminLocation()
    {
        var (sut, world, playerController, _) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.Guardian;
        admin.CurrentMap = FakeMap.Create();
        admin.Character.X = 3;
        admin.Character.Y = 4;

        var (target, _) = FakePlayer.Create("Target", 2);
        world.FindPlayerByName("Target").Returns(target);

        await sut.SummonPlayerAsync(admin, "Target");

        await playerController.Received(1).WarpAsync(target, admin.CurrentMap, 3, 4, WarpEffect.Admin);
    }

    [Test]
    public async Task WarpToPlayer_WhenTargetIsSelf_DoesNotWarp()
    {
        var (sut, world, playerController, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.LightGuide;
        admin.CurrentMap = FakeMap.Create();
        world.FindPlayerByName("Admin").Returns(admin);

        await sut.WarpToPlayerAsync(admin, "Admin");

        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
        await notifications.Received(1).SystemMessage(admin, "You cannot warp to yourself.");
    }

    [Test]
    public async Task SummonPlayer_WhenTargetIsSelf_DoesNotWarp()
    {
        var (sut, world, playerController, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.Guardian;
        admin.CurrentMap = FakeMap.Create();
        world.FindPlayerByName("Admin").Returns(admin);

        await sut.SummonPlayerAsync(admin, "Admin");

        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
        await notifications.Received(1).SystemMessage(admin, "You cannot summon yourself.");
    }

    [Test]
    public async Task SummonPlayer_WhenTargetOffline_DoesNotWarp()
    {
        var (sut, world, playerController, notifications) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.Guardian;
        admin.CurrentMap = FakeMap.Create();
        world.FindPlayerByName("Ghost").Returns((PlayerState?)null);

        await sut.SummonPlayerAsync(admin, "Ghost");

        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
        await notifications.Received(1).SystemMessage(admin, "Player 'Ghost' is not online.");
    }

    [Test]
    public async Task SummonPlayer_WhenAdminBelowGuardian_DoesNothing()
    {
        var (sut, world, playerController, _) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.LightGuide;
        admin.CurrentMap = FakeMap.Create();

        await sut.SummonPlayerAsync(admin, "Target");

        world.DidNotReceive().FindPlayerByName(Arg.Any<string>());
        await playerController.DidNotReceiveWithAnyArgs().WarpAsync(default!, default!, default, default, default);
    }
}
