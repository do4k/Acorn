using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildTellClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildTellClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildTellClientPacket packet)
    {
        await guildService.GetGuildMemberList(player, packet.SessionId, packet.GuildIdentity);
    }
}
