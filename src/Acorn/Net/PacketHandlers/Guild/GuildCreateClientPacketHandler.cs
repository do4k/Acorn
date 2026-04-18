using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildCreateClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildCreateClientPacketHandler> logger)
    : IPacketHandler<GuildCreateClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildCreateClientPacket packet)
    {
        await guildService.FinishGuildCreation(player, packet.SessionId, packet.GuildTag, packet.GuildName, packet.Description);
    }
}
