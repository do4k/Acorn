using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
internal class TalkAdminClientPacketHandler(
    IWorldQueries world,
    ILogger<TalkAdminClientPacketHandler> logger) : IPacketHandler<TalkAdminClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkAdminClientPacket packet)
    {
        if (playerState.Character!.Admin < AdminLevel.Guardian)
        {
            logger.LogDebug("Player tried to send an admin message without admin permissions {Player}",
                playerState.Character.Name);
            return;
        }

        var broadcast = world.GetAllPlayers()
            .Where(x => x != playerState && x.Character?.Admin >= AdminLevel.Guardian)
            .Select(x => x.Send(new TalkAdminServerPacket
            {
                Message = packet.Message,
                PlayerName = playerState.Character.Name!
            }));

        await Task.WhenAll(broadcast);
    }
}
