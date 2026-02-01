using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Board;

public class BoardRemoveClientPacketHandler(
    ILogger<BoardRemoveClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardRemoveClientPacket>
{
    private const int MaxPosts = 20;

    public async Task HandleAsync(PlayerState player, BoardRemoveClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to remove board post without character or map", player.SessionId);
            return;
        }

        var boardId = packet.BoardId;
        var postId = packet.PostId;

        logger.LogInformation("Player {Character} attempting to remove post {PostId} from board {BoardId}",
            player.Character.Name, postId, boardId);

        // Only admins can remove posts (following reoserv behavior)
        if ((int)player.Character.Admin < 1)
        {
            logger.LogWarning("Player {Character} tried to remove post without admin privileges",
                player.Character.Name);
            await RefreshBoard(player, boardId);
            return;
        }

        // Validate board ID (1-8)
        if (boardId < 1 || boardId > 8)
        {
            logger.LogWarning("Player {Character} tried to remove post from invalid board {BoardId}",
                player.Character.Name, boardId);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = GetBoardTileSpec(boardId);
        if (boardTileSpec == null)
        {
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap.Data, player.Character.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to remove post from board {BoardId} but not in range",
                player.Character.Name, boardId);
            return;
        }

        // Delete the post
        await boardRepository.DeletePostAsync(postId);

        logger.LogInformation("Admin {Character} removed post {PostId} from board {BoardId}",
            player.Character.Name, postId, boardId);

        // Refresh the board
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

}
