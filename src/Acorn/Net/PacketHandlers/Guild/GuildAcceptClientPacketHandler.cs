using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildAcceptClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildAcceptClientPacketHandler> logger)
    : IPacketHandler<GuildAcceptClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildAcceptClientPacket packet)
    {
        await guildService.AcceptGuildCreation(player, packet.InviterPlayerId);
    }
}
