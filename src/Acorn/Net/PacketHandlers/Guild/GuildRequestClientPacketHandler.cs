using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildRequestClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildRequestClientPacketHandler> logger)
    : IPacketHandler<GuildRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildRequestClientPacket packet)
    {
        await guildService.CreateGuildRequest(player, packet.SessionId, packet.GuildTag, packet.GuildName);
    }
}
