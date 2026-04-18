using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildKickClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildKickClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildKickClientPacket packet)
    {
        await guildService.KickFromGuild(player, packet.SessionId, packet.MemberName);
    }
}
