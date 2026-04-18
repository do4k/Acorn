using Acorn.World.Services.Guild;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Guild;

[RequiresCharacter]
public class GuildAgreeClientPacketHandler(
    IGuildService guildService)
    : IPacketHandler<GuildAgreeClientPacket>
{
    public async Task HandleAsync(PlayerState player, GuildAgreeClientPacket packet)
    {
        if (packet.InfoTypeData is null) return;

        switch (packet.InfoTypeData)
        {
            case GuildAgreeClientPacket.InfoTypeDataDescription descData:
                await guildService.UpdateGuildDescription(player, packet.SessionId, descData.Description);
                break;
            case GuildAgreeClientPacket.InfoTypeDataRanks ranksData:
                await guildService.UpdateGuildRanks(player, packet.SessionId, [.. ranksData.Ranks]);
                break;
        }
    }
}
