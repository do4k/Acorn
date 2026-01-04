using Acorn.World;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Board;

public class BoardOpenClientPacketHandler(
    ILogger<BoardOpenClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IMapTileService tileService)
    : IPacketHandler<BoardOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BoardOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open board without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening board {BoardId}",
            player.Character.Name, packet.BoardId);

        // Validate board ID (1-8)
        if (packet.BoardId < 1 || packet.BoardId > 8)
        {
            logger.LogWarning("Player {Character} tried to open invalid board {BoardId}",
                player.Character.Name, packet.BoardId);
            return;
        }

        // Get corresponding MapTileSpec for the board
        var boardTileSpec = packet.BoardId switch
        {
            1 => MapTileSpec.Board1,
            2 => MapTileSpec.Board2,
            3 => MapTileSpec.Board3,
            4 => MapTileSpec.Board4,
            5 => MapTileSpec.Board5,
            6 => MapTileSpec.Board6,
            7 => MapTileSpec.Board7,
            8 => MapTileSpec.Board8,
            _ => (MapTileSpec?)null
        };

        if (boardTileSpec == null)
        {
            return;
        }

        // Check if player is in range of the board tile
        if (!tileService.PlayerInRangeOfTile(player.CurrentMap.Data, player.Character.AsCoords(), boardTileSpec.Value))
        {
            logger.LogWarning("Player {Character} tried to open board {BoardId} but not in range",
                player.Character.Name, packet.BoardId);
            return;
        }

        // TODO: Fetch board posts from database
        // TODO: Send BoardOpenServerPacket with posts
        logger.LogInformation("Player {Character} successfully opened board {BoardId}",
            player.Character.Name, packet.BoardId);

        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (BoardOpenClientPacket)packet);
    }
}