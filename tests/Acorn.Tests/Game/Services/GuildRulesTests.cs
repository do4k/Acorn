using Acorn.Options;
using Acorn.World.Services.Guild;
using FluentAssertions;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class GuildRulesTests
{
    private readonly GuildOptions _options = new();

    // --- Validation ---

    [Theory]
    [InlineData("AB", true)]
    [InlineData("ABC", true)]
    [InlineData("A", false)]
    [InlineData("ABCD", false)]
    [InlineData("A1C", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void IsValidTag_ShouldMatchEoservRules(string tag, bool expected)
    {
        GuildRules.IsValidTag(tag, _options).Should().Be(expected);
    }

    [Theory]
    [InlineData("test", true)]
    [InlineData("my guild", true)]
    [InlineData("abc", false)]
    [InlineData("Guild", false)]
    [InlineData("guild1", false)]
    [InlineData("guild-name", false)]
    [InlineData("", false)]
    public void IsValidName_ShouldMatchEoservRules(string name, bool expected)
    {
        GuildRules.IsValidName(name, _options).Should().Be(expected);
    }

    [Fact]
    public void IsValidName_WhenAtMaximumLength_ShouldBeValid()
    {
        var name = new string('a', _options.MaxNameLength);
        GuildRules.IsValidName(name, _options).Should().BeTrue();
    }

    [Fact]
    public void IsValidName_WhenOverMaximumLength_ShouldBeInvalid()
    {
        var name = new string('a', _options.MaxNameLength + 1);
        GuildRules.IsValidName(name, _options).Should().BeFalse();
    }

    [Theory]
    [InlineData("leader", true)]
    [InlineData("new member", true)]
    [InlineData("", true)]
    [InlineData("Leader", false)]
    [InlineData("rank1", false)]
    public void IsValidRank_ShouldMatchEoservRules(string rank, bool expected)
    {
        GuildRules.IsValidRank(rank, _options).Should().Be(expected);
    }

    [Fact]
    public void IsValidRank_WhenOverMaximumLength_ShouldBeInvalid()
    {
        GuildRules.IsValidRank(new string('a', _options.MaxRankLength + 1), _options).Should().BeFalse();
    }

    [Theory]
    [InlineData("hello world", true)]
    [InlineData("hello@world.com", true)]
    [InlineData("a_b-c.1", true)]
    [InlineData("hello!", false)]
    [InlineData("Hello", false)]
    [InlineData("", true)]
    public void IsValidDescription_ShouldMatchEoservRules(string description, bool expected)
    {
        GuildRules.IsValidDescription(description, _options).Should().Be(expected);
    }

    [Fact]
    public void IsValidDescription_WhenOverMaximumLength_ShouldBeInvalid()
    {
        GuildRules.IsValidDescription(new string('a', _options.MaxDescLength + 1), _options).Should().BeFalse();
    }

    // --- Permissions ---

    [Fact]
    public void CanEdit_ShouldOnlyAllowLeader_WithEoservDefaults()
    {
        GuildRules.CanEdit(0, _options).Should().BeTrue();
        GuildRules.CanEdit(1, _options).Should().BeFalse();
        GuildRules.CanEdit(8, _options).Should().BeFalse();
    }

    [Fact]
    public void CanRecruit_ShouldAllowLeaderAndRecruiter_WithEoservDefaults()
    {
        GuildRules.CanRecruit(0, _options).Should().BeTrue();
        GuildRules.CanRecruit(1, _options).Should().BeTrue();
        GuildRules.CanRecruit(2, _options).Should().BeFalse();
    }

    [Fact]
    public void CanKick_ShouldOnlyAllowLeader_WithEoservDefaults()
    {
        GuildRules.CanKick(0, _options).Should().BeTrue();
        GuildRules.CanKick(1, _options).Should().BeFalse();
    }

    [Fact]
    public void CanDisband_ShouldOnlyAllowLeader_WithEoservDefaults()
    {
        GuildRules.CanDisband(0, _options).Should().BeTrue();
        GuildRules.CanDisband(1, _options).Should().BeFalse();
    }

    [Fact]
    public void CanAssignRank_LeaderCanAssignRankZero_WhenMultipleFoundersEnabled()
    {
        GuildRules.CanAssignRank(0, 8, 0, _options).Should().BeTrue();
    }

    [Fact]
    public void CanAssignRank_LeaderCannotAssignRankZero_WhenMultipleFoundersDisabled()
    {
        var options = new GuildOptions { MultipleFounders = false };
        GuildRules.CanAssignRank(0, 8, 0, options).Should().BeFalse();
    }

    [Fact]
    public void CanAssignRank_NonLeaderCannotAssignRankZero()
    {
        GuildRules.CanAssignRank(1, 8, 0, _options).Should().BeFalse();
    }

    [Fact]
    public void CanAssignRank_LeaderCanPromoteMember()
    {
        GuildRules.CanAssignRank(0, 8, 5, _options).Should().BeTrue();
    }

    [Fact]
    public void CanAssignRank_NonLeaderCannotManageMember()
    {
        GuildRules.CanAssignRank(1, 8, 5, _options).Should().BeFalse();
    }

    [Fact]
    public void CanAssignRank_CannotAssignToExistingLeader()
    {
        GuildRules.CanAssignRank(0, 0, 5, _options).Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void CanAssignRank_OutOfRangeRank_ShouldBeFalse(int newRank)
    {
        GuildRules.CanAssignRank(0, 8, newRank, _options).Should().BeFalse();
    }

    // --- Wealth / staff ---

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1999, "1999")]
    [InlineData(123456, "123456")]
    public void GetWealth_ShouldReportRawBankBalance(int bank, string expected)
    {
        GuildRules.GetWealth(bank).Should().Be(expected);
    }

    [Fact]
    public void GetStaff_ShouldCategoriseLeadersAndRecruitersLikeEoserv()
    {
        var members = new List<(int Rank, string Name)>
        {
            (0, "Alice"),
            (1, "Bob"),
            (2, "Carol"),
            (8, "Dave")
        };

        var staff = GuildRules.GetStaff(members, _options);

        staff.Should().HaveCount(2);
        staff[0].Rank.Should().Be(1);
        staff[0].Name.Should().Be("Alice (founder)");
        staff[1].Rank.Should().Be(2);
        staff[1].Name.Should().Be("Bob");
    }

    [Fact]
    public void GetStaff_WhenShowRecruitersDisabled_ShouldOnlyIncludeLeaders()
    {
        var options = new GuildOptions { ShowRecruiters = false };
        var members = new List<(int Rank, string Name)> { (0, "Alice"), (1, "Bob") };

        var staff = GuildRules.GetStaff(members, options);

        staff.Should().ContainSingle();
        staff[0].Name.Should().Be("Alice (founder)");
    }

    // --- Bank deposit ---

    [Fact]
    public void CalculateDeposit_WhenValid_ShouldReturnRequestedAmount()
    {
        GuildRules.CalculateDeposit(5000, 10000, 0, _options).Should().Be(5000);
    }

    [Fact]
    public void CalculateDeposit_WhenBelowMinimum_ShouldReturnZero()
    {
        GuildRules.CalculateDeposit(500, 10000, 0, _options).Should().Be(0);
    }

    [Fact]
    public void CalculateDeposit_WhenPlayerHasLessThanMinimum_ShouldReturnZero()
    {
        GuildRules.CalculateDeposit(5000, 500, 0, _options).Should().Be(0);
    }

    [Fact]
    public void CalculateDeposit_WhenRemainingCapacityBelowMinimum_ShouldReturnZero()
    {
        var bank = _options.BankMax - 100;
        GuildRules.CalculateDeposit(5000, 10000, bank, _options).Should().Be(0);
    }

    [Fact]
    public void CalculateDeposit_WhenBankFull_ShouldReturnZero()
    {
        GuildRules.CalculateDeposit(5000, 10000, _options.BankMax, _options).Should().Be(0);
    }

    [Fact]
    public void CalculateDeposit_ShouldClampToRemainingCapacity()
    {
        var bank = _options.BankMax - 2000;
        GuildRules.CalculateDeposit(5000, 10000, bank, _options).Should().Be(2000);
    }

    [Fact]
    public void CalculateDeposit_ShouldClampToPlayerGold()
    {
        GuildRules.CalculateDeposit(5000, 2000, 0, _options).Should().Be(2000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CalculateDeposit_WhenNonPositiveRequested_ShouldReturnZero(int requested)
    {
        GuildRules.CalculateDeposit(requested, 10000, 0, _options).Should().Be(0);
    }

    // --- Creation flow ---

    [Fact]
    public void RequiresRecruits_WhenCreateMembersIsOne_ShouldBeFalse()
    {
        GuildRules.RequiresRecruits(_options).Should().BeFalse();
    }

    [Fact]
    public void RequiresRecruits_WhenCreateMembersGreaterThanOne_ShouldBeTrue()
    {
        GuildRules.RequiresRecruits(new GuildOptions { CreateMembers = 3 }).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 0, true)] // solo creation needs no candidates
    [InlineData(3, 1, false)]
    [InlineData(3, 2, true)]
    [InlineData(3, 5, true)]
    public void HasEnoughCandidates_ShouldReflectCreateMembers(int createMembers, int candidateCount, bool expected)
    {
        var options = new GuildOptions { CreateMembers = createMembers };
        GuildRules.HasEnoughCandidates(candidateCount, options).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(3, 1, false)]
    [InlineData(3, 2, true)]
    [InlineData(9, 8, true)]
    public void HasEnoughMembers_ShouldCountLeaderAndRecruits(int createMembers, int recruitCount, bool expected)
    {
        var options = new GuildOptions { CreateMembers = createMembers };
        GuildRules.HasEnoughMembers(recruitCount, options).Should().Be(expected);
    }

    // --- Ranks ---

    [Fact]
    public void ParseRanks_ShouldReturnNineRanks()
    {
        var ranks = GuildRules.ParseRanks(_options.DefaultRanks);

        ranks.Should().HaveCount(9);
        ranks[0].Should().Be("Leader");
        ranks[1].Should().Be("Recruiter");
        ranks[8].Should().Be("New Member");
    }

    [Fact]
    public void GetRankName_WhenOutOfRange_ShouldReturnEmpty()
    {
        var ranks = GuildRules.ParseRanks(_options.DefaultRanks);

        GuildRules.GetRankName(ranks, -1).Should().BeEmpty();
        GuildRules.GetRankName(ranks, 9).Should().BeEmpty();
    }
}
