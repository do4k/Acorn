using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildTakeClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildTakeClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildTakeClientPacket packet)
    {
        await guildService.GetGuildInfoByType(player, packet.SessionId, (int)packet.InfoType);
    }
}
