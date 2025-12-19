using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopCreateClientPacketHandler(ILogger<ShopCreateClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<ShopCreateClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopCreateClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to create shop without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} crafting item {CraftItemId}",
            player.Character.Name, packet.CraftItemId);

        // TODO: Implement map.CraftItem(player, craftItemId)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ShopCreateClientPacket)packet);
    }
}
