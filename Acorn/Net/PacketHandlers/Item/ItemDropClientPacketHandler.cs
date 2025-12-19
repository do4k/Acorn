using Acorn.Database.Repository;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemDropClientPacketHandler(
    ILogger<ItemDropClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IDbRepository<Database.Models.Character> characterRepository)
    : IPacketHandler<ItemDropClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemDropClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to drop item without character or map", player.SessionId);
            return;
        }

        // Convert ByteCoords to Coords
        var coords = new Moffat.EndlessOnline.SDK.Protocol.Coords { X = packet.Coords.X, Y = packet.Coords.Y };
        
        // Use map's drop item method which handles all validation and broadcasting
        var success = await player.CurrentMap.DropItem(player, packet.Item.Id, packet.Item.Amount, coords);
        
        // Save character inventory to database if successful
        if (success)
        {
            await characterRepository.UpdateAsync(player.Character.AsDatabaseModel());
        }
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemDropClientPacket)packet);
    }
}
