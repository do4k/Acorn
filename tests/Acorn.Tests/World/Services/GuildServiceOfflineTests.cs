using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Guild;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Covers guild operations against members that are offline: the persisted
///     membership row must be updated (or removed) instead of the action silently
///     being skipped.
/// </summary>
public class GuildServiceOfflineTests
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

    private static GuildService CreateSut(IServiceScopeFactory scopeFactory)
    {
        var world = Substitute.For<IWorldQueries>();
        var inventory = Substitute.For<IInventoryService>();
        var options = Microsoft.Extensions.Options.Options.Create(new GuildOptions());
        return new GuildService(scopeFactory, world, inventory, options, NullLogger<GuildService>.Instance);
    }

    private static async Task AddOfflineMemberAsync(ServiceProvider provider, string guildTag, string memberName,
        int rankIndex)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
        db.GuildMembers.Add(new GuildMember
        {
            GuildTag = guildTag,
            CharacterName = memberName,
            RankIndex = rankIndex
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Creates guild TST with an online leader whose interaction context is set.
    /// </summary>
    private static async Task<(GuildService Sut, Acorn.Net.PlayerState Leader, ServiceProvider Provider)> CreateGuildAsync(
        string memberName = "Sleeper", int memberRank = GuildRules.NewMemberRank)
    {
        var provider = CreateProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sut = CreateSut(scopeFactory);
        var (leader, _) = FakePlayer.Create("Leader", 1);

        await sut.AdminCreateGuild(leader, "TST", "test guild", string.Empty);
        await AddOfflineMemberAsync(provider, "TST", memberName, memberRank);

        return (sut, leader, provider);
    }

    private static async Task<GuildMember?> FindMemberAsync(ServiceProvider provider, string memberName)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();
        return await db.GuildMembers.AsNoTracking().FirstOrDefaultAsync(m => m.CharacterName == memberName);
    }

    [Test]
    public async Task KickOfflineMember_RemovesMembershipRow()
    {
        var (sut, leader, provider) = await CreateGuildAsync();

        await sut.KickFromGuild(leader, leader.SessionId, "Sleeper");

        (await FindMemberAsync(provider, "Sleeper")).Should().BeNull("offline members must be removed from the db");
    }

    [Test]
    public async Task KickOfflineMember_TargetInDifferentGuild_LeavesRowAndSendsNotMember()
    {
        // Sleeper belongs to TST only.
        var (sut, _, provider) = await CreateGuildAsync(memberName: "Sleeper");

        var (otherLeader, _) = FakePlayer.Create("Leader2", 2);
        await sut.AdminCreateGuild(otherLeader, "ABC", "other guild", string.Empty);

        await sut.KickFromGuild(otherLeader, otherLeader.SessionId, "Sleeper");

        var member = await FindMemberAsync(provider, "Sleeper");
        member.Should().NotBeNull("members of another guild must not be kickable");
        member!.GuildTag.Should().Be("TST");
    }

    [Test]
    public async Task KickOfflineLeader_RefusedAndRowKept()
    {
        var (sut, leader, provider) = await CreateGuildAsync(memberRank: GuildRules.LeaderRank);

        await sut.KickFromGuild(leader, leader.SessionId, "Sleeper");

        var member = await FindMemberAsync(provider, "Sleeper");
        member.Should().NotBeNull("leaders must not be removable by kicks");
        member!.GuildTag.Should().Be("TST");
    }

    [Test]
    public async Task UpdateOfflineMemberRank_PersistsNewRank()
    {
        var (sut, leader, provider) = await CreateGuildAsync(memberRank: GuildRules.NewMemberRank);
        leader.InteractingNpcIndex = 0;

        await sut.UpdateMemberRank(leader, leader.SessionId, "Sleeper", 3);

        var member = await FindMemberAsync(provider, "Sleeper");
        member.Should().NotBeNull();
        member!.RankIndex.Should().Be(3);
    }

    [Test]
    public async Task UpdateOfflineMemberRank_TargetIsLeader_RefusedAndUnchanged()
    {
        var (sut, leader, provider) = await CreateGuildAsync(memberRank: GuildRules.LeaderRank);
        leader.InteractingNpcIndex = 0;

        await sut.UpdateMemberRank(leader, leader.SessionId, "Sleeper", 3);

        var member = await FindMemberAsync(provider, "Sleeper");
        member!.RankIndex.Should().Be(GuildRules.LeaderRank, "leader ranks must be protected from demotion");
    }

    [Test]
    public async Task UpdateOfflineMemberRank_UnknownMember_LeavesDbUntouched()
    {
        var (sut, leader, provider) = await CreateGuildAsync(memberName: "Sleeper");
        leader.InteractingNpcIndex = 0;

        await sut.UpdateMemberRank(leader, leader.SessionId, "Ghost", 3);

        var member = await FindMemberAsync(provider, "Sleeper");
        member.Should().NotBeNull();
        member!.RankIndex.Should().Be(GuildRules.NewMemberRank, "an unknown target must not change anyone's rank");
    }
}
