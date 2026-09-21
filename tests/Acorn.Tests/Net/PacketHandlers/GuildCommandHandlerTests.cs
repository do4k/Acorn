using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World.Services.Guild;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;

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
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", string.Empty)
            .Returns(AdminCreateGuildResult.Created);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "my", "guild");

        await notifications.Received(1)
            .SystemMessage(
                Arg.Any<PlayerState>(),
                Arg.Is<string>(m => m.Contains("TST") && m.Contains("leader")));
    }

    [Test]
    public async Task Handle_CreateJoinsNameWordsAndPassesEmptyDescription()
    {
        var (sut, guildService, _) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", string.Empty)
            .Returns(AdminCreateGuildResult.Created);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "my", "guild");

        await guildService.Received(1)
            .AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", string.Empty);
    }

    [Test]
    public async Task Handle_CreateWithSeparator_ParsesNameAndDescription()
    {
        var (sut, guildService, _) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", "a cool guild")
            .Returns(AdminCreateGuildResult.Created);

        await sut.HandleAsync(
            Admin(), "guild", "create", "TST", "my", "guild", "--", "a", "cool", "guild");

        await guildService.Received(1)
            .AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", "a cool guild");
    }

    [Test]
    public async Task Handle_CreateWithSeparatorButNoName_ShowsUsage()
    {
        var (sut, guildService, notifications) = CreateSut();

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "--", "a description");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Usage")));
        await guildService.DidNotReceive()
            .AdminCreateGuild(Arg.Any<PlayerState>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Handle_CreateWithSeparatorButNoDescription_ShowsUsage()
    {
        var (sut, guildService, notifications) = CreateSut();

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "my", "guild", "--");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Usage")));
        await guildService.DidNotReceive()
            .AdminCreateGuild(Arg.Any<PlayerState>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Handle_CreateInvalidInput_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "A", "my guild", string.Empty)
            .Returns(AdminCreateGuildResult.InvalidInput);

        await sut.HandleAsync(Admin(), "guild", "create", "A", "my", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("Invalid")));
    }

    [Test]
    public async Task Handle_AlreadyInGuild_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", string.Empty)
            .Returns(AdminCreateGuildResult.AlreadyInGuild);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "my", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("already")));
    }

    [Test]
    public async Task Handle_GuildExists_SendsError()
    {
        var (sut, guildService, notifications) = CreateSut();
        guildService.AdminCreateGuild(Arg.Any<PlayerState>(), "TST", "my guild", string.Empty)
            .Returns(AdminCreateGuildResult.GuildExists);

        await sut.HandleAsync(Admin(), "guild", "create", "TST", "my", "guild");

        await notifications.Received(1)
            .SystemMessage(Arg.Any<PlayerState>(), Arg.Is<string>(m => m.Contains("already exists")));
    }
}
