using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildJunkClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildJunkClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildJunkClientPacket packet)
    {
        await guildService.DisbandGuild(player, packet.SessionId);
    }
}
