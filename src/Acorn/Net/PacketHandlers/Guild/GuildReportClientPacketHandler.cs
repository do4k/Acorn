using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildReportClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildReportClientPacketHandler> logger)
    : IPacketHandler<GuildReportClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildReportClientPacket packet)
    {
        await guildService.GetGuildInfo(player, packet.SessionId, packet.GuildIdentity);
    }
}
