using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Shop;

public class ShopOpenClientPacketHandler(ILogger<ShopOpenClientPacketHandler> logger)
    : IPacketHandler<ShopOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, ShopOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open shop without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening shop at NPC {NpcIndex}",
            player.Character.Name, packet.NpcIndex);

        // TODO: Implement map.OpenShop(player, npcIndex)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (ShopOpenClientPacket)packet);
    }
}