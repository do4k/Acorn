using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Book;

[RequiresCharacter]
public class BookRequestClientPacketHandler(ILogger<BookRequestClientPacketHandler> logger)
    : IPacketHandler<BookRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, BookRequestClientPacket packet)
    {
        logger.LogInformation("Player {Character} requesting book from player {PlayerId}",
            player.Character!.Name, packet.PlayerId);

        // TODO: Implement map.RequestBook(player, playerId)
        await Task.CompletedTask;
    }

}