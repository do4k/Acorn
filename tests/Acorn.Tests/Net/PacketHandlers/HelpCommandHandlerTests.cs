using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

public class HelpCommandHandlerTests
{
    private sealed class StubAdminHandler(IReadOnlyList<string> commands, string usage, AdminLevel level)
        : ITalkHandler
    {
        public IReadOnlyList<string> Commands { get; } = commands;
        public string Usage { get; } = usage;
        public AdminLevel RequiredLevel { get; } = level;

        public Task HandleAsync(PlayerState playerState, string command, params string[] args)
            => Task.CompletedTask;
    }

    private sealed class StubPlayerHandler(IReadOnlyList<string> commands) : IPlayerCommandHandler
    {
        public IReadOnlyList<string> Commands { get; } = commands;

        public Task HandleAsync(PlayerState playerState, string command, params string[] args)
            => Task.CompletedTask;
    }

    private static (HelpCommandHandler Sut, INotificationService Notifications) CreateSut()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITalkHandler>(
            new StubAdminHandler(["warp"], "<map id or name>", AdminLevel.Spy));
        services.AddSingleton<ITalkHandler>(
            new StubAdminHandler(["nuke"], "", AdminLevel.HighGameMaster));
        services.AddSingleton<IPlayerCommandHandler>(new StubPlayerHandler(["loc"]));
        var provider = services.BuildServiceProvider();

        var notifications = Substitute.For<INotificationService>();
        return (new HelpCommandHandler(provider, notifications), notifications);
    }

    private static PlayerState Admin(AdminLevel level)
    {
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = level;
        return admin;
    }

    [Test]
    public async Task Help_ListsCommandsAtOrBelowCallerLevel()
    {
        var (sut, notifications) = CreateSut();
        var admin = Admin(AdminLevel.GameMaster);

        await sut.HandleAsync(admin, "help");

        await notifications.Received()
            .SystemMessage(admin, Arg.Is<string>(m => m.Contains("$warp")));
        await notifications.Received()
            .SystemMessage(admin, Arg.Is<string>(m => m.Contains("#loc")));
        await notifications.DidNotReceive()
            .SystemMessage(admin, Arg.Is<string>(m => m.Contains("$nuke")));
    }

    [Test]
    public async Task Help_WithCommandArgument_ShowsUsage()
    {
        var (sut, notifications) = CreateSut();
        var admin = Admin(AdminLevel.GameMaster);

        await sut.HandleAsync(admin, "help", "warp");

        await notifications.Received(1)
            .SystemMessage(admin, Arg.Is<string>(m => m.StartsWith("$warp <map id or name>")));
    }

    [Test]
    public async Task Help_WithUnknownCommand_ReportsNotFound()
    {
        var (sut, notifications) = CreateSut();
        var admin = Admin(AdminLevel.GameMaster);

        await sut.HandleAsync(admin, "help", "doesnotexist");

        await notifications.Received(1)
            .SystemMessage(admin, "No command named 'doesnotexist'.");
    }
}
