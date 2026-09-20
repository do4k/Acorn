using Acorn.Infrastructure;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Admin;
using FluentAssertions;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

/// <summary>
///     Tests for the admin command handlers added alongside the warp toolkit
///     ($wmt/$warpmeto, $summon/$bring/$warptome), plus $who/$online, $uptime,
///     $repub and $rehash.
/// </summary>
public class CommandHandlerTests
{
    // --- $wmt / $warpmeto ---

    [Test]
    public async Task WarpMeTo_WithoutTarget_ShowsUsageAndDoesNotWarp()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new WarpMeToCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "wmt");

        await adminService.DidNotReceiveWithAnyArgs().WarpToPlayerAsync(default!, default!);
        await notifications.Received(1).SystemMessage(player, "Usage: $wmt <player>");
    }

    [Test]
    public async Task WarpMeTo_WithTarget_WarpsToPlayer()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new WarpMeToCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "warpmeto", "Bob");

        await adminService.Received(1).WarpToPlayerAsync(player, "Bob");
    }

    // --- $summon / $bring / $warptome ---

    [Test]
    public async Task Summon_WithoutTarget_ShowsUsageAndDoesNotWarp()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new SummonCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "summon");

        await adminService.DidNotReceiveWithAnyArgs().SummonPlayerAsync(default!, default!);
        await notifications.Received(1).SystemMessage(player, "Usage: $summon <player>");
    }

    [Test]
    public async Task Summon_WithTarget_WarpsPlayerToAdmin()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new SummonCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "warptome", "Bob");

        await adminService.Received(1).SummonPlayerAsync(player, "Bob");
    }

    // --- Silent command variants ---

    [Test]
    public async Task Ban_WhenSilentAlias_DoesNotAnnounce()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new BanCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "sban", "Bob");

        await adminService.Received(1).BanPlayerAsync(player, "Bob", true);
    }

    [Test]
    public async Task Ban_WhenNormalAlias_Announces()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new BanCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "ban", "Bob");

        await adminService.Received(1).BanPlayerAsync(player, "Bob", false);
    }

    [Test]
    public async Task Kick_WhenSilentAlias_DoesNotAnnounce()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new KickCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "skick", "Bob");

        await adminService.Received(1).KickPlayerAsync(player, "Bob", true);
    }

    [Test]
    public async Task Jail_WhenSilentAlias_DoesNotAnnounce()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new JailCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "sjail", "Bob");

        await adminService.Received(1).JailPlayerAsync(player, "Bob", true);
    }

    [Test]
    public async Task Mute_WhenSilentAlias_DoesNotAnnounce()
    {
        var adminService = Substitute.For<IAdminService>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new MuteCommandHandler(adminService, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "smute", "Bob");

        await adminService.Received(1).MutePlayerAsync(player, "Bob", true);
    }

    // --- $who / $online ---

    [Test]
    public async Task Who_ListsOnlinePlayersWithLocation()
    {
        var world = Substitute.For<IWorldQueries>();
        var notifications = Substitute.For<INotificationService>();
        var handler = new WhoCommandHandler(world, notifications);
        var (admin, _) = FakePlayer.Create("Admin", 1);

        var (bob, _) = FakePlayer.Create("Bob", 2);
        bob.CurrentMap = FakeMap.Create();
        bob.Character!.Map = 1;
        bob.Character.X = 7;
        bob.Character.Y = 9;

        var (carol, _) = FakePlayer.Create("Carol", 3);
        carol.Character!.Map = 3;
        carol.Character.X = 1;
        carol.Character.Y = 2;

        world.GetAllPlayers().Returns([bob, carol]);

        await handler.HandleAsync(admin, "who");

        await notifications.Received(1).SystemMessage(admin, "Online players (2):");
        await notifications.Received(1).SystemMessage(admin, "  Bob - Map 1 (TestMap) @ 7,9");
        await notifications.Received(1).SystemMessage(admin, "  Carol - Map 3 @ 1,2");
    }

    // --- $uptime ---

    [Test]
    public async Task Uptime_ReportsElapsedTime()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = start;
        var serverStatus = new ServerStatusService(() => now);
        var notifications = Substitute.For<INotificationService>();
        var handler = new UptimeCommandHandler(serverStatus, notifications);
        var (player, _) = FakePlayer.Create();

        now = start.AddDays(2).AddHours(3).AddMinutes(4).AddSeconds(5);
        await handler.HandleAsync(player, "uptime");

        await notifications.Received(1).SystemMessage(player, "Uptime: 2d 3h 4m 5s");
    }

    // --- $repub ---

    [Test]
    public async Task Repub_WhenReloadSucceeds_ReportsSuccess()
    {
        var pubFileReload = Substitute.For<IPubFileReloadService>();
        pubFileReload.ReloadAsync().Returns(true);
        var notifications = Substitute.For<INotificationService>();
        var handler = new RepubCommandHandler(pubFileReload, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "repub");

        await pubFileReload.Received(1).ReloadAsync();
        await notifications.Received(1).SystemMessage(player, "Pub files reloaded.");
    }

    [Test]
    public async Task Repub_WhenReloadFails_ReportsFailure()
    {
        var pubFileReload = Substitute.For<IPubFileReloadService>();
        pubFileReload.ReloadAsync().Returns(false);
        var notifications = Substitute.For<INotificationService>();
        var handler = new RepubCommandHandler(pubFileReload, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "repub");

        await notifications.Received(1)
            .SystemMessage(player, "Failed to reload pub files - check the server log.");
    }

    // --- $rehash ---

    [Test]
    public async Task Rehash_WhenBothReloadsSucceed_ReportsWithRestartCaveat()
    {
        var configurationReload = Substitute.For<IConfigurationReloadService>();
        configurationReload.Reload().Returns(true);
        var pubFileReload = Substitute.For<IPubFileReloadService>();
        pubFileReload.ReloadAsync().Returns(true);
        var notifications = Substitute.For<INotificationService>();
        var handler = new RehashCommandHandler(configurationReload, pubFileReload, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "rehash");

        await notifications.Received(1)
            .SystemMessage(player,
                "Configuration reloaded and pub files refreshed. Settings bound at startup still require a restart.");
    }

    [Test]
    public async Task Rehash_WhenOnlyPubReloads_ReportsPartialFailure()
    {
        var configurationReload = Substitute.For<IConfigurationReloadService>();
        configurationReload.Reload().Returns(false);
        var pubFileReload = Substitute.For<IPubFileReloadService>();
        pubFileReload.ReloadAsync().Returns(true);
        var notifications = Substitute.For<INotificationService>();
        var handler = new RehashCommandHandler(configurationReload, pubFileReload, notifications);
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, "rehash");

        await notifications.Received(1)
            .SystemMessage(player, "Pub files refreshed, but configuration failed to reload - check the server log.");
    }
}
