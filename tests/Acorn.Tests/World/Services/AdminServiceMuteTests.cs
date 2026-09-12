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
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.World.Services;

public class AdminServiceMuteTests
{
    private static (AdminService Sut, PlayerState Target) CreateSut(int muteLengthSeconds = 90)
    {
        var world = Substitute.For<IWorldQueries>();
        var playerController = Substitute.For<IPlayerController>();
        var notifications = Substitute.For<INotificationService>();
        var banService = Substitute.For<IBanService>();
        var serverOptions = FakePlayer.CreateOptions();
        serverOptions.MuteLengthSeconds = muteLengthSeconds;
        var options = Microsoft.Extensions.Options.Options.Create(serverOptions);
        var logger = NullLogger<AdminService>.Instance;
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        var sut = new AdminService(world, playerController, notifications, banService, options, scopeFactory, logger);

        var (target, _) = FakePlayer.Create("Target", 2);
        world.FindPlayerByName("Target").Returns(target);
        world.GetAllPlayers().Returns(Array.Empty<PlayerState>());

        return (sut, target);
    }

    [Test]
    public async Task MutePlayer_SetsTimedMute_NotPermanent()
    {
        var (sut, target) = CreateSut(muteLengthSeconds: 120);
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.GameMaster;

        var before = DateTime.UtcNow;
        await sut.MutePlayerAsync(admin, "Target");

        target.IsMuted.Should().BeTrue();
        target.MutedUntil.Should().BeCloseTo(before.AddSeconds(120), TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task UnmutePlayer_ClearsMute()
    {
        var (sut, target) = CreateSut();
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.GameMaster;

        await sut.MutePlayerAsync(admin, "Target");
        target.IsMuted.Should().BeTrue();

        await sut.UnmutePlayerAsync(admin, "Target");
        target.IsMuted.Should().BeFalse();
    }

    [Test]
    public void ExpiredMute_IsNotMuted()
    {
        var (_, target) = CreateSut();
        target.MutedUntil = DateTime.UtcNow.AddSeconds(-1);

        target.IsMuted.Should().BeFalse();
    }
}