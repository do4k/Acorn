using Acorn.World.Services.Guild;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildRankClientPacketHandler(
    IGuildService guildService,
    ILogger<GuildRankClientPacketHandler> logger)
    : IPacketHandler<GuildRankClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildRankClientPacket packet)
    {
        await guildService.UpdateMemberRank(player, packet.SessionId, packet.MemberName, packet.Rank);
    }
}
