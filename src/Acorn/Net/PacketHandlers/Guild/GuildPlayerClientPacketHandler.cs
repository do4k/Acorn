using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildPlayerClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildPlayerClientPacketHandler> logger)
    : IPacketHandler<GuildPlayerClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildPlayerClientPacket packet)
    {
        await guildService.RequestToJoinGuild(player, packet.SessionId, packet.GuildTag, packet.RecruiterName);
    }
}
