using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildBuyClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildBuyClientPacketHandler> logger)
    : IPacketHandler<GuildBuyClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildBuyClientPacket packet)
    {
        await guildService.DepositGuildGold(player, packet.SessionId, packet.GoldAmount);
    }
}
