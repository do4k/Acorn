using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildBuyClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildBuyClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildBuyClientPacket packet)
    {
        await guildService.DepositGuildGold(player, packet.SessionId, packet.GoldAmount);
    }
}
