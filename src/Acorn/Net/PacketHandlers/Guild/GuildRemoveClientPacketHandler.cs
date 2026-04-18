using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildRemoveClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildRemoveClientPacketHandler> logger)
    : IPacketHandler<GuildRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildRemoveClientPacket packet)
    {
        await guildService.LeaveGuild(player, packet.SessionId);
    }
}
