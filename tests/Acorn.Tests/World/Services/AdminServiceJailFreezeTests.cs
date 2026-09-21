using Acorn.Net;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Admin;
using Acorn.World.Services.Bans;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Verifies that jail/freeze state is written to the character as well as the
///     live session, so relogging does not clear it.
/// </summary>
public class AdminServiceJailFreezeTests
{
    private static (AdminService Sut, PlayerState Target) CreateSut()
    {
        var world = Substitute.For<IWorldQueries>();
        var playerController = Substitute.For<IPlayerController>();
        var notifications = Substitute.For<INotificationService>();
        var banService = Substitute.For<IBanService>();
        var options = Microsoft.Extensions.Options.Options.Create(FakePlayer.CreateOptions());
        var logger = NullLogger<AdminService>.Instance;
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var sut = new AdminService(world, playerController, notifications, banService, options, scopeFactory, logger);

        world.FindMap(Arg.Any<int>()).Returns(FakeMap.Create());

        var (target, _) = FakePlayer.Create("Target", 2);
        world.FindPlayerByName("Target").Returns(target);
        world.GetAllPlayers().Returns(Array.Empty<PlayerState>());

        return (sut, target);
    }

    private static PlayerState Admin()
    {
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.GameMaster;
        return admin;
    }

    [Test]
    public async Task Jail_ShouldPersistOnCharacter()
    {
        var (sut, target) = CreateSut();

        await sut.JailPlayerAsync(Admin(), "Target");

        target.IsJailed.Should().BeTrue();
        target.Character!.Jailed.Should().BeTrue();
    }

    [Test]
    public async Task Free_ShouldClearPersistedJail()
    {
        var (sut, target) = CreateSut();
        target.IsJailed = true;
        target.Character!.Jailed = true;

        await sut.FreePlayerAsync(Admin(), "Target");

        target.IsJailed.Should().BeFalse();
        target.Character.Jailed.Should().BeFalse();
    }

    [Test]
    public async Task Freeze_ShouldPersistOnCharacter()
    {
        var (sut, target) = CreateSut();

        await sut.FreezePlayerAsync(Admin(), "Target");

        target.IsFrozen.Should().BeTrue();
        target.Character!.Frozen.Should().BeTrue();
    }

    [Test]
    public async Task Unfreeze_ShouldClearPersistedFreeze()
    {
        var (sut, target) = CreateSut();
        target.IsFrozen = true;
        target.Character!.Frozen = true;

        await sut.UnfreezePlayerAsync(Admin(), "Target");

        target.IsFrozen.Should().BeFalse();
        target.Character.Frozen.Should().BeFalse();
    }
}
