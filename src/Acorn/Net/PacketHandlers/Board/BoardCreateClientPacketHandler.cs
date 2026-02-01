using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Board;

public class BoardCreateClientPacketHandler(
    ILogger<BoardCreateClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardCreateClientPacket>
{
    private const int MaxPosts = 20;
    private const int MaxSubjectLength = 64;
    private const int MaxBodyLength = 2048;
    private const int MaxRecentPosts = 2; // Max posts within recent time window
    private const int MaxUserPosts = 3; // Max posts by user on board
    private static readonly TimeSpan RecentPostWindow = TimeSpan.FromMinutes(10);

    public async Task HandleAsync(PlayerState player, BoardCreateClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to create board post without character or map", player.SessionId);
            return;
        }

        var boardId = packet.BoardId;

        // Validate board ID (1-8)
        if (boardId < 1 || boardId > 8)
        {
            logger.LogWarning("Player {Character} tried to post to invalid board {BoardId}",
                player.Character.Name, boardId);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = GetBoardTileSpec(boardId);
        if (boardTileSpec == null)
        {
            await RefreshBoard(player, boardId);
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap.Data, player.Character.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to post to board {BoardId} but not in range",
                player.Character.Name, boardId);
            await RefreshBoard(player, boardId);
            return;
        }

        // Truncate subject and body if too long
        var subject = packet.PostSubject?.Length > MaxSubjectLength
            ? packet.PostSubject[..MaxSubjectLength]
            : packet.PostSubject ?? "";

        var body = packet.PostBody?.Length > MaxBodyLength
            ? packet.PostBody[..MaxBodyLength]
            : packet.PostBody ?? "";

        // Check for empty content
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Player {Character} tried to create empty post on board {BoardId}",
                player.Character.Name, boardId);
            await RefreshBoard(player, boardId);
            return;
        }

        // Check rate limits
        var recentPosts = await boardRepository.GetRecentPostCountAsync(boardId, player.Character.Name!, RecentPostWindow);
        var totalPosts = await boardRepository.GetTotalPostCountAsync(boardId, player.Character.Name!, MaxPosts);

        if (recentPosts >= MaxRecentPosts || totalPosts >= MaxUserPosts)
        {
            logger.LogWarning("Player {Character} hit post limit on board {BoardId} (recent: {Recent}, total: {Total})",
                player.Character.Name, boardId, recentPosts, totalPosts);
            await RefreshBoard(player, boardId);
            return;
        }

        // Create the post
        var post = new BoardPost
        {
            BoardId = boardId,
            CharacterName = player.Character.Name!,
            Subject = subject,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        await boardRepository.CreatePostAsync(post);

        logger.LogInformation("Player {Character} created post '{Subject}' on board {BoardId}",
            player.Character.Name, subject, boardId);

        // Refresh the board to show the new post
        await RefreshBoard(player, boardId);
    }

    private static MapTileSpec? GetBoardTileSpec(int boardId) => boardId switch
    {
        1 => MapTileSpec.Board1,
        2 => MapTileSpec.Board2,
        3 => MapTileSpec.Board3,
        4 => MapTileSpec.Board4,
        5 => MapTileSpec.Board5,
        6 => MapTileSpec.Board6,
        7 => MapTileSpec.Board7,
        8 => MapTileSpec.Board8,
        _ => null
    };

    private async Task RefreshBoard(PlayerState player, int boardId)
    {
        var posts = await boardRepository.GetPostsAsync(boardId, MaxPosts);
        var postListings = posts.Select(p => new BoardPostListing
        {
            PostId = p.Id,
            Author = p.CharacterName,
            Subject = p.Subject
        }).ToList();

        await player.Send(new BoardOpenServerPacket
        {
            BoardId = boardId,
            Posts = postListings
        });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (BoardCreateClientPacket)packet);
    }
}
