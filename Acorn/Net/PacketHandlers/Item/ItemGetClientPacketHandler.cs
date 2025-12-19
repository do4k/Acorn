using Acorn.Database.Repository;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemGetClientPacketHandler(
    ILogger<ItemGetClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemGetClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemGetClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to get item without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} picking up item at index {ItemIndex}",
            player.Character.Name, packet.ItemIndex);

        // Use map's get item method which handles all validation and broadcasting
        await player.CurrentMap.GetItem(player, packet.ItemIndex);
        
        // Save character inventory to database
        await characterRepository.UpdateAsync(player.Character.AsDatabaseModel());
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemGetClientPacket)packet);
    }
}
