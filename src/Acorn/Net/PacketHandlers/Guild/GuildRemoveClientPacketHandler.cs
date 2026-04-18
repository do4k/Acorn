using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildRemoveClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildRemoveClientPacket packet)
    {
        await guildService.LeaveGuild(player, packet.SessionId);
    }
}
