using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildUseClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildUseClientPacketHandler> logger)
    : IPacketHandler<GuildUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildUseClientPacket packet)
    {
        await guildService.AcceptJoinRequest(player, packet.PlayerId);
    }
}
