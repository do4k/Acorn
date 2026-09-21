using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World.Services.Guild;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Acorn.Tests.Net.PacketHandlers;

public class GuildCommandHandlerTests
{
    private static (GuildCommandHandler Sut, IGuildService GuildService, INotificationService Notifications) CreateSut()
    {
        var guildService = Substitute.For<IGuildService>();
        var notifications = Substitute.For<INotificationService>();
        var sut = new GuildCommandHandler(guildService, notifications, NullLogger<GuildCommandHandler>.Instance);
        return (sut, guildService, notifications);
    }

    private static PlayerState Admin()
    {
        var (admin, _) = FakePlayer.Create("Admin", 1);
        admin.Character!.Admin = AdminLevel.GameMaster;
        return admin;
    }

    [Test]
    public async Task Handle_WithNoArgs_ShowsUsage()
    {
        var (sut, _, notifications) = CreateSut();

        await sut.HandleAsync(Admin(), "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Usage")));
    }

    [Test]
    public async Task Handle_WithMissingNameArgs_ShowsUsage()
    {
        var (sut, _, notifications) = CreateSut();

        await sut.HandleAsync(Admin(), "guild", "create", "TST");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Usage")));
    }

    [Test]
    public async Task Handle_UnknownSubcommand_ShowsUsage()
    {
        var (sut, _, notifications) = CreateSut();

        await sut.HandleAsync(Admin(), "guild", "list");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Usage")));
    }

    [Test]
    public async Task Handle_CreateSuccess_SendsConfirmation()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "test guild")
            .Returns(AdminCreateGuildResult.Created);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "test", "guild");

        await notifications.Received(1)
            .SystemMessage(
                Arg.Any<PlayerState>(),
                Arg.Is<string>(m => m.Contains("TST") && m.Contains("leader")));
    }

    [Test]
    public async Task Handle_CreateInvalidTag_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "A", "test guild")
            .Returns(AdminCreateGuildResult.InvalidTagOrName);

        await sut.HandleAsync(Admin(), "guild", "create", "A", "test", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Invalid")));
    }

    [Test]
    public async Task Handle_AlreadyInGuild_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "test guild")
            .Returns(AdminCreateGuildResult.AlreadyInGuild);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "test", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("already")));
    }

    [Test]
    public async Task Handle_GuildExists_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "test guild")
            .Returns(AdminCreateGuildResult.GuildExists);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "test", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("already exists")));
    }

    [Test]
    public async Task Handle_Create_PassesArgsToService()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "test guild")
            .Returns(AdminCreateGuildResult.Created);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "test", "guild");

        await guildService.Received(1)
            .AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "test guild");
    }
}
