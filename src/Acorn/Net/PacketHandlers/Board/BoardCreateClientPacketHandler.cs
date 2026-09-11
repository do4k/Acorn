using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Board;

[RequiresCharacter]
public class BoardCreateClientPacketHandler(
    ILogger<BoardCreateClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardCreateClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardCreateClientPacket packet)
    {
        var boardId = packet.BoardId;

        // Validate board ID (0-7, where 0 maps to Board1)
        if (!BoardRules.IsValidBoardId(boardId))
        {
            logger.LogWarning("Player {Character} tried to post to invalid board {BoardId}",
                player.Character!.Name, boardId);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = BoardRules.GetTileSpec(boardId);
        if (boardTileSpec == null)
        {
            await RefreshBoard(player, boardId);
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap!.Data, player.Character!.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to post to board {BoardId} but not in range",
                player.Character!.Name, boardId);
            await RefreshBoard(player, boardId);
            return;
        }

        // Sanitize (replace the reserved 0xFF byte) and truncate subject and body
        var subject = BoardRules.SanitizeContent(packet.PostSubject, BoardRules.MaxSubjectLength);
        var body = BoardRules.SanitizeContent(packet.PostBody, BoardRules.MaxBodyLength);

        // Check for empty content
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Player {Character} tried to create empty post on board {BoardId}",
                player.Character!.Name, boardId);
            await RefreshBoard(player, boardId);
            return;
        }

        // Check rate limits
        var recentPosts = await boardRepository.GetRecentPostCountAsync(boardId, player.Character!.Name!, BoardRules.RecentPostWindow);
        var totalPosts = await boardRepository.GetTotalPostCountAsync(boardId, player.Character!.Name!);

        if (recentPosts >= BoardRules.MaxRecentPosts || totalPosts >= BoardRules.MaxUserPosts)
        {
            logger.LogWarning("Player {Character} hit post limit on board {BoardId} (recent: {Recent}, total: {Total})",
                player.Character!.Name, boardId, recentPosts, totalPosts);
            await RefreshBoard(player, boardId);
            return;
        }

        // Create the post
        var post = new BoardPost
        {
            BoardId = boardId,
            CharacterName = player.Character!.Name!,
            AuthorAdmin = player.Character!.Admin,
            Subject = subject,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        await boardRepository.CreatePostAsync(post);

        logger.LogInformation("Player {Character} created post '{Subject}' on board {BoardId}",
            player.Character!.Name, subject, boardId);

        // Refresh the board to show the new post
        await RefreshBoard(player, boardId);
    }

    private async Task RefreshBoard(PlayerState player, int boardId)
    {
        var posts = await boardRepository.GetPostsAsync(boardId, BoardRules.GetPostLimit(boardId));
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

}
