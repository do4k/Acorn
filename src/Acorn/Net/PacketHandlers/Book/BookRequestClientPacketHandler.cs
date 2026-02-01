using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Book;

public class BookRequestClientPacketHandler(ILogger<BookRequestClientPacketHandler> logger)
    : IPacketHandler<BookRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, BookRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to request book without character or map",
                player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} requesting book from player {PlayerId}",
            player.Character.Name, packet.PlayerId);

        // TODO: Implement map.RequestBook(player, playerId)
        await Task.CompletedTask;
    }

}