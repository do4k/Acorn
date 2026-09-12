using Acorn.Net.PacketHandlers.Board;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.Tests.Game.Board;

public class BoardRulesTests
{
    [Test]
    [Arguments(0, MapTileSpec.Board1)]
    [Arguments(1, MapTileSpec.Board2)]
    [Arguments(2, MapTileSpec.Board3)]
    [Arguments(3, MapTileSpec.Board4)]
    [Arguments(4, MapTileSpec.Board5)]
    [Arguments(5, MapTileSpec.Board6)]
    [Arguments(6, MapTileSpec.Board7)]
    [Arguments(7, MapTileSpec.Board8)]
    public void GetTileSpec_WhenGivenValidBoardId_ShouldMapToBoardTile(int boardId, MapTileSpec expected)
    {
        BoardRules.GetTileSpec(boardId).Should().Be(expected);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(8)]
    [Arguments(100)]
    public void GetTileSpec_WhenGivenOutOfRangeBoardId_ShouldReturnNull(int boardId)
    {
        BoardRules.GetTileSpec(boardId).Should().BeNull();
    }

    [Test]
    [Arguments(0, true)]
    [Arguments(7, true)]
    [Arguments(-1, false)]
    [Arguments(8, false)]
    public void IsValidBoardId_ShouldOnlyAcceptZeroBasedIds(int boardId, bool expected)
    {
        BoardRules.IsValidBoardId(boardId).Should().Be(expected);
    }

    [Test]
    public void AdminBoardId_ShouldBeTheLastZeroBasedBoard()
    {
        BoardRules.AdminBoardId.Should().Be(7);
        BoardRules.IsAdminBoard(7).Should().BeTrue();
        BoardRules.GetTileSpec(BoardRules.AdminBoardId).Should().Be(MapTileSpec.Board8);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public void IsAdminBoard_WhenGivenNonAdminBoard_ShouldBeFalse(int boardId)
    {
        BoardRules.IsAdminBoard(boardId).Should().BeFalse();
    }

    [Test]
    public void GetPostLimit_ShouldUseLargerLimitForAdminBoard()
    {
        BoardRules.GetPostLimit(0).Should().Be(BoardRules.MaxPosts);
        BoardRules.GetPostLimit(BoardRules.AdminBoardId).Should().Be(BoardRules.AdminBoardMaxPosts);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(1, true)]
    [Arguments(5, true)]
    public void CanAccessAdminBoard_ShouldRequireAtLeastAdminLevelOne(int adminLevel, bool expected)
    {
        BoardRules.CanAccessAdminBoard(adminLevel).Should().Be(expected);
    }

    [Test]
    public void CanRemovePost_WhenPlayerIsAuthor_ShouldAllowEvenWithoutAdmin()
    {
        BoardRules.CanRemovePost(currentAdminLevel: 0, authorAdminLevel: 5, isAuthor: true)
            .Should().BeTrue();
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(4, 3)]
    [Arguments(5, 0)]
    public void CanRemovePost_WhenAdminIsHigherRankedThanAuthor_ShouldAllow(int currentAdminLevel, int authorAdminLevel)
    {
        BoardRules.CanRemovePost(currentAdminLevel, authorAdminLevel, isAuthor: false)
            .Should().BeTrue();
    }

    [Test]
    [Arguments(1, 1)]
    [Arguments(0, 0)]
    [Arguments(0, 1)]
    [Arguments(2, 3)]
    public void CanRemovePost_WhenNonAuthorIsNotHigherRanked_ShouldDeny(int currentAdminLevel, int authorAdminLevel)
    {
        BoardRules.CanRemovePost(currentAdminLevel, authorAdminLevel, isAuthor: false)
            .Should().BeFalse();
    }

    [Test]
    public void SanitizeContent_ShouldReplaceReserved0XffByte()
    {
        BoardRules.SanitizeContent("hello\u00FFworld", BoardRules.MaxBodyLength)
            .Should().Be("helloyworld");
    }

    [Test]
    public void SanitizeContent_ShouldTruncateToMaxLength()
    {
        var value = new string('a', BoardRules.MaxSubjectLength + 10);

        BoardRules.SanitizeContent(value, BoardRules.MaxSubjectLength)
            .Should().HaveLength(BoardRules.MaxSubjectLength);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public void SanitizeContent_WhenNullOrEmpty_ShouldReturnEmpty(string? value)
    {
        BoardRules.SanitizeContent(value, BoardRules.MaxBodyLength).Should().BeEmpty();
    }
}