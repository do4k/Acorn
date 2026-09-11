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
public class BoardRemoveClientPacketHandler(
    ILogger<BoardRemoveClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardRemoveClientPacket packet)
    {
        var boardId = packet.BoardId;
        var postId = packet.PostId;

        logger.LogInformation("Player {Character} attempting to remove post {PostId} from board {BoardId}",
            player.Character!.Name, postId, boardId);

        // Validate board ID (0-7, where 0 maps to Board1)
        if (!BoardRules.IsValidBoardId(boardId))
        {
            logger.LogWarning("Player {Character} tried to remove post from invalid board {BoardId}",
                player.Character!.Name, boardId);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = BoardRules.GetTileSpec(boardId);
        if (boardTileSpec == null)
        {
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap!.Data, player.Character!.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to remove post from board {BoardId} but not in range",
                player.Character!.Name, boardId);
            return;
        }

        // Look up the post so we can check ownership
        var post = await boardRepository.GetPostAsync(boardId, postId);
        if (post == null)
        {
            logger.LogWarning("Player {Character} tried to remove non-existent post {PostId} from board {BoardId}",
                player.Character!.Name, postId, boardId);
            return;
        }

        // The author may delete their own post; an admin may delete posts by a lower-ranked author.
        var isAuthor = string.Equals(post.CharacterName, player.Character!.Name, StringComparison.Ordinal);
        if (!BoardRules.CanRemovePost((int)player.Character!.Admin, (int)post.AuthorAdmin, isAuthor))
        {
            logger.LogWarning(
                "Player {Character} tried to remove post {PostId} by {Author} without permission",
                player.Character!.Name, postId, post.CharacterName);
            await RefreshBoard(player, boardId);
            return;
        }

        // Delete the post
        await boardRepository.DeletePostAsync(postId);

        logger.LogInformation("Player {Character} removed post {PostId} from board {BoardId}",
            player.Character!.Name, postId, boardId);

        // Refresh the board
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
