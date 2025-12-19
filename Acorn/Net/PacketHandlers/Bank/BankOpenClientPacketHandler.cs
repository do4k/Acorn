using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Bank;

public class BankOpenClientPacketHandler(ILogger<BankOpenClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<BankOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BankOpenClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to open bank without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} opening bank at NPC {NpcIndex}",
            player.Character.Name, packet.NpcIndex);

        // TODO: Validate NPC exists and is a banker
        // TODO: Validate player is close enough to NPC
        // TODO: Send BankOpen server packet with:
        //       - Bank items: player.Character.Bank.Items
        //       - Bank capacity: player.Character.BankMax
        //       - Gold in bank: player.Character.GoldBank
        
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (BankOpenClientPacket)packet);
    }
}
