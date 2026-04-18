using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildOpenClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildOpenClientPacketHandler> logger)
    : IPacketHandler<GuildOpenClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildOpenClientPacket packet)
    {
        await guildService.OpenGuildMaster(player, packet.NpcIndex);
    }
}
