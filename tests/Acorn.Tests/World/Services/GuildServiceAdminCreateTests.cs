using Acorn.Database;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Options;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Guild;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acorn.Tests.World.Services;

public class GuildServiceAdminCreateTests
{
    private static ServiceProvider CreateProvider()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AcornDbContext>(options => options.UseSqlite(connection));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AcornDbContext>().Database.EnsureCreated();

        return provider;
    }

    private static GuildService CreateSut(IServiceScopeFactory scopeFactory, IInventoryService? inventoryService = null)
    {
        var world = Substitute.For<IWorldQueries>();
        var inventory = inventoryService ?? Substitute.For<IInventoryService>();
        var options = Microsoft.Extensions.Options.Options.Create(new GuildOptions());
        return new GuildService(scopeFactory, world, inventory, options, NullLogger<GuildService>.Instance);
    }

    [Test]
    public async Task AdminCreate_ValidInput_CreatesGuildAndMembershipInDb()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        var result = await sut.AdminCreateGuild(player, "TST", "test guild");

        result.Should().Be(AdminCreateGuildResult.Created);

        using var verify = provider.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FindAsync("TST");
        guild.Should().NotBeNull();
        guild!.Name.Should().Be("test guild");

        var member = await db.GuildMembers.FirstOrDefaultAsync(m => m.GuildTag == "TST");
        member.Should().NotBeNull();
        member!.CharacterName.Should().Be("Leader");
        member.RankIndex.Should().Be(GuildRules.LeaderRank);
    }

    [Test]
    public async Task AdminCreate_ValidInput_UpdatesInMemoryCharacter()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        var result = await sut.AdminCreateGuild(player, "TST", "test guild");

        result.Should().Be(AdminCreateGuildResult.Created);
        player.Character!.GuildTag.Should().Be("TST");
        player.Character.GuildName.Should().Be("test guild");
        player.Character.GuildRankIndex.Should().Be(GuildRules.LeaderRank);
    }

    [Test]
    public async Task AdminCreate_PlayerAlreadyInGuild_ReturnsAlreadyInGuild()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        await sut.AdminCreateGuild(player, "TST", "test guild");

        var result = await sut.AdminCreateGuild(player, "ABC", "another guild");

        result.Should().Be(AdminCreateGuildResult.AlreadyInGuild);
    }

    [Test]
    public async Task AdminCreate_TagTaken_ReturnsGuildExists()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player1, _) = FakePlayer.Create("Leader1", 1);
        var (player2, _) = FakePlayer.Create("Leader2", 2);

        await sut.AdminCreateGuild(player1, "TST", "test guild");

        var result = await sut.AdminCreateGuild(player2, "TST", "different name");

        result.Should().Be(AdminCreateGuildResult.GuildExists);
    }

    [Test]
    public async Task AdminCreate_NameTaken_ReturnsGuildExists()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player1, _) = FakePlayer.Create("Leader1", 1);
        var (player2, _) = FakePlayer.Create("Leader2", 2);

        await sut.AdminCreateGuild(player1, "TST", "test guild");

        var result = await sut.AdminCreateGuild(player2, "ABC", "test guild");

        result.Should().Be(AdminCreateGuildResult.GuildExists);
    }

    [Test]
    [Arguments("A")]
    [Arguments("ABCD")]
    public async Task AdminCreate_InvalidTag_ReturnsInvalidTagOrName(string badTag)
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        var result = await sut.AdminCreateGuild(player, badTag, "test guild");

        result.Should().Be(AdminCreateGuildResult.InvalidTagOrName);
    }

    [Test]
    [Arguments("abc")]
    [Arguments("ab1c")]
    public async Task AdminCreate_InvalidName_ReturnsInvalidTagOrName(string badName)
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        var result = await sut.AdminCreateGuild(player, "TST", badName);

        result.Should().Be(AdminCreateGuildResult.InvalidTagOrName);
    }

    [Test]
    public async Task AdminCreate_NormalizesTagAndName()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (player, _) = FakePlayer.Create("Leader", 1);

        await sut.AdminCreateGuild(player, " ts ", "  My Guild  ");

        player.Character!.GuildTag.Should().Be("TS");
        player.Character.GuildName.Should().Be("my guild");
    }

    [Test]
    public async Task AdminCreate_SendsGuildCreateServerPacket()
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var inventoryService = Substitute.For<IInventoryService>();
        inventoryService.GetItemAmount(Arg.Any<Character>(), Arg.Any<int>()).Returns(12345);
        var sut = CreateSut(scopeFactory, inventoryService);
        var (player, communicator) = FakePlayer.Create("Leader", 1);

        await sut.AdminCreateGuild(player, "TST", "test guild");

        communicator.Sent.Should().NotBeEmpty("a GuildCreateServerPacket should have been sent");
    }
}
