using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.World;
using Acorn.World.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemGetClientPacketHandler(
    ILogger<ItemGetClientPacketHandler> logger,
    IMapItemService mapItemService,
    ICharacterMapper characterMapper,
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

        // Use map item service for pickup logic
        var result = await mapItemService.TryPickupItem(player, player.CurrentMap, packet.ItemIndex);
        
        if (result.Success)
        {
            // Save character inventory to database
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemGetClientPacket)packet);
    }
}
