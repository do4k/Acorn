using Acorn.Net.PacketHandlers.Board;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Xunit;

namespace Acorn.Tests.Game.Board;

public class BoardRulesTests
{
    [Theory]
    [InlineData(0, MapTileSpec.Board1)]
    [InlineData(1, MapTileSpec.Board2)]
    [InlineData(2, MapTileSpec.Board3)]
    [InlineData(3, MapTileSpec.Board4)]
    [InlineData(4, MapTileSpec.Board5)]
    [InlineData(5, MapTileSpec.Board6)]
    [InlineData(6, MapTileSpec.Board7)]
    [InlineData(7, MapTileSpec.Board8)]
    public void GetTileSpec_WhenGivenValidBoardId_ShouldMapToBoardTile(int boardId, MapTileSpec expected)
    {
        BoardRules.GetTileSpec(boardId).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(100)]
    public void GetTileSpec_WhenGivenOutOfRangeBoardId_ShouldReturnNull(int boardId)
    {
        BoardRules.GetTileSpec(boardId).Should().BeNull();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(8, false)]
    public void IsValidBoardId_ShouldOnlyAcceptZeroBasedIds(int boardId, bool expected)
    {
        BoardRules.IsValidBoardId(boardId).Should().Be(expected);
    }

    [Fact]
    public void AdminBoardId_ShouldBeTheLastZeroBasedBoard()
    {
        BoardRules.AdminBoardId.Should().Be(7);
        BoardRules.IsAdminBoard(7).Should().BeTrue();
        BoardRules.GetTileSpec(BoardRules.AdminBoardId).Should().Be(MapTileSpec.Board8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void IsAdminBoard_WhenGivenNonAdminBoard_ShouldBeFalse(int boardId)
    {
        BoardRules.IsAdminBoard(boardId).Should().BeFalse();
    }

    [Fact]
    public void GetPostLimit_ShouldUseLargerLimitForAdminBoard()
    {
        BoardRules.GetPostLimit(0).Should().Be(BoardRules.MaxPosts);
        BoardRules.GetPostLimit(BoardRules.AdminBoardId).Should().Be(BoardRules.AdminBoardMaxPosts);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void CanAccessAdminBoard_ShouldRequireAtLeastAdminLevelOne(int adminLevel, bool expected)
    {
        BoardRules.CanAccessAdminBoard(adminLevel).Should().Be(expected);
    }

    [Fact]
    public void CanRemovePost_WhenPlayerIsAuthor_ShouldAllowEvenWithoutAdmin()
    {
        BoardRules.CanRemovePost(currentAdminLevel: 0, authorAdminLevel: 5, isAuthor: true)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 3)]
    [InlineData(5, 0)]
    public void CanRemovePost_WhenAdminIsHigherRankedThanAuthor_ShouldAllow(int currentAdminLevel, int authorAdminLevel)
    {
        BoardRules.CanRemovePost(currentAdminLevel, authorAdminLevel, isAuthor: false)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void CanRemovePost_WhenNonAuthorIsNotHigherRanked_ShouldDeny(int currentAdminLevel, int authorAdminLevel)
    {
        BoardRules.CanRemovePost(currentAdminLevel, authorAdminLevel, isAuthor: false)
            .Should().BeFalse();
    }

    [Fact]
    public void SanitizeContent_ShouldReplaceReserved0XffByte()
    {
        BoardRules.SanitizeContent("hello\u00FFworld", BoardRules.MaxBodyLength)
            .Should().Be("helloyworld");
    }

    [Fact]
    public void SanitizeContent_ShouldTruncateToMaxLength()
    {
        var value = new string('a', BoardRules.MaxSubjectLength + 10);

        BoardRules.SanitizeContent(value, BoardRules.MaxSubjectLength)
            .Should().HaveLength(BoardRules.MaxSubjectLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SanitizeContent_WhenNullOrEmpty_ShouldReturnEmpty(string? value)
    {
        BoardRules.SanitizeContent(value, BoardRules.MaxBodyLength).Should().BeEmpty();
    }
}
