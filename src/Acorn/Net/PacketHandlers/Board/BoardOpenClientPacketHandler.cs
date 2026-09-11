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
public class BoardOpenClientPacketHandler(
    ILogger<BoardOpenClientPacketHandler> logger,
    IMapTileService tileService,
    IBoardRepository boardRepository)
    : IPacketHandler<BoardOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardOpenClientPacket packet)
    {
        logger.LogInformation("Player {Character} opening board {BoardId}",
            player.Character!.Name, packet.BoardId);

        // Validate board ID (0-7, where 0 maps to Board1)
        if (!BoardRules.IsValidBoardId(packet.BoardId))
        {
            logger.LogWarning("Player {Character} tried to open invalid board {BoardId}",
                player.Character!.Name, packet.BoardId);
            return;
        }

        // Check admin board permissions
        if (BoardRules.IsAdminBoard(packet.BoardId) &&
            !BoardRules.CanAccessAdminBoard((int)player.Character!.Admin))
        {
            logger.LogWarning("Player {Character} tried to open admin board without permission",
                player.Character!.Name);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = BoardRules.GetTileSpec(packet.BoardId);
        if (boardTileSpec == null)
        {
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap!.Data, player.Character!.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to open board {BoardId} but not in range",
                player.Character!.Name, packet.BoardId);
            return;
        }

        // Store the board ID for subsequent operations
        player.InteractingBoardId = packet.BoardId;

        // Fetch board posts from database
        var posts = await boardRepository.GetPostsAsync(packet.BoardId, BoardRules.GetPostLimit(packet.BoardId));

        // Build post listings
        var postListings = posts.Select(p => new BoardPostListing
        {
            PostId = p.Id,
            Author = p.CharacterName,
            Subject = p.Subject
        }).ToList();

        await player.Send(new BoardOpenServerPacket
        {
            BoardId = packet.BoardId,
            Posts = postListings
        });

        logger.LogInformation("Player {Character} successfully opened board {BoardId} with {PostCount} posts",
            player.Character!.Name, packet.BoardId, postListings.Count);
    }

}
