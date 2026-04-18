using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildAcceptClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildAcceptClientPacket packet)
    {
        await guildService.AcceptGuildCreation(player, packet.InviterPlayerId);
    }
}
