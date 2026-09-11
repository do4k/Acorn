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
public class BoardTakeClientPacketHandler(
    ILogger<BoardTakeClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardTakeClientPacket packet)
    {
        var boardId = packet.BoardId;
        var postId = packet.PostId;

        logger.LogInformation("Player {Character} reading post {PostId} from board {BoardId}",
            player.Character!.Name, postId, boardId);

        // Validate board ID (0-7, where 0 maps to Board1)
        if (!BoardRules.IsValidBoardId(boardId))
        {
            logger.LogWarning("Player {Character} tried to read post from invalid board {BoardId}",
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
            logger.LogWarning("Player {Character} tried to read post from board {BoardId} but not in range",
                player.Character!.Name, boardId);
            return;
        }

        // Fetch the post
        var post = await boardRepository.GetPostAsync(boardId, postId);
        if (post == null)
        {
            logger.LogWarning("Player {Character} tried to read non-existent post {PostId} from board {BoardId}",
                player.Character!.Name, postId, boardId);
            return;
        }

        // Send the post body to the player
        await player.Send(new BoardPlayerServerPacket
        {
            PostId = postId,
            PostBody = post.Body
        });

        logger.LogInformation("Player {Character} read post {PostId} from board {BoardId}",
            player.Character!.Name, postId, boardId);
    }

}
