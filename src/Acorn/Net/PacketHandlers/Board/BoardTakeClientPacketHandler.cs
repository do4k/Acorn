using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Board;

public class BoardTakeClientPacketHandler(
    ILogger<BoardTakeClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardTakeClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to read board post without character or map", player.SessionId);
            return;
        }

        var boardId = packet.BoardId;
        var postId = packet.PostId;

        logger.LogInformation("Player {Character} reading post {PostId} from board {BoardId}",
            player.Character.Name, postId, boardId);

        // Validate board ID (1-8)
        if (boardId < 1 || boardId > 8)
        {
            logger.LogWarning("Player {Character} tried to read post from invalid board {BoardId}",
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
            logger.LogWarning("Player {Character} tried to read post from board {BoardId} but not in range",
                player.Character.Name, boardId);
            return;
        }

        // Fetch the post
        var post = await boardRepository.GetPostAsync(boardId, postId);
        if (post == null)
        {
            logger.LogWarning("Player {Character} tried to read non-existent post {PostId} from board {BoardId}",
                player.Character.Name, postId, boardId);
            return;
        }

        // Send the post body to the player
        await player.Send(new BoardPlayerServerPacket
        {
            PostId = postId,
            PostBody = post.Body
        });

        logger.LogInformation("Player {Character} read post {PostId} from board {BoardId}",
            player.Character.Name, postId, boardId);
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

}
