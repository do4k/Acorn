using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Bank;

[RequiresCharacter]
public class BankOpenClientPacketHandler(
    ILogger<BankOpenClientPacketHandler> logger)
    : IPacketHandler<BankOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, BankOpenClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateAndStartInteraction(player, packet.NpcIndex, NpcType.Bank, logger);
        if (npc is null) return;

        logger.LogInformation("Player {Character} opening bank",
            player.Character.Name);

        await player.Send(new BankOpenServerPacket
        {
            GoldBank = player.Character.GoldBank,
            SessionId = player.SessionId,
            LockerUpgrades = player.Character.BankMax / 5 // BankMax represents locker slots, upgrades are in increments of 5
        });
    }

}
