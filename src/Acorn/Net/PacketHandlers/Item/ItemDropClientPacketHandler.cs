using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.World;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemDropClientPacketHandler(
    ILogger<ItemDropClientPacketHandler> logger,
    IMapItemService mapItemService,
    ICharacterMapper characterMapper,
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

        // Use map item service for drop logic
        var result = await mapItemService.TryDropItem(player, player.CurrentMap, packet.Item.Id, packet.Item.Amount, coords);

        // Save character inventory to database if successful
        if (result.Success)
        {
            await characterRepository.UpdateAsync(characterMapper.ToDatabase(player.Character));
        }
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemDropClientPacket)packet);
    }
}
