using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Message;

public class MessagePingClientPacketHandler(ILogger<MessagePingClientPacketHandler> logger)
    : IPacketHandler<MessagePingClientPacket>
{
    public async Task HandleAsync(PlayerState player, MessagePingClientPacket packet)
    {
        if (player.Character == null)
        {
            logger.LogWarning("Player {SessionId} attempted to send message without character", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} sent ping",
            player.Character.Name);

        // TODO: Respond with pong packet
        await Task.CompletedTask;
    }

}