using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Message;

[RequiresCharacter]
public class MessagePingClientPacketHandler(ILogger<MessagePingClientPacketHandler> logger)
    : IPacketHandler<MessagePingClientPacket>
{
    public async Task HandleAsync(PlayerState player, MessagePingClientPacket packet)
    {
        logger.LogDebug("Player {Character} sent ping",
            player.Character!.Name);

        await player.Send(new MessagePongServerPacket());
    }

}