using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkReportClientPacketHandler(IEnumerable<ITalkHandler> talkHandlers, ILogger<TalkReportClientPacketHandler> logger)
    : IPacketHandler<TalkReportClientPacket>
{
    public async Task HandleAsync(ConnectionHandler connectionHandler,
        TalkReportClientPacket packet)
    {
        var author = connectionHandler.CharacterController;

        if (author?.Data.Admin > AdminLevel.Player && packet.Message.StartsWith("$"))
        {
            var args = packet.Message.Split(" ");
            var command = args[0][1..];

            var handler = talkHandlers.FirstOrDefault(x => x.CanHandle(command));
            if (handler is null)
            {
                return;
            }

            await handler.HandleAsync(connectionHandler, command, args[1..]);
            return;
        }

        if (connectionHandler.CurrentMap is null)
        {
            logger.LogError("Tried to handle talk report packet, but the map for the player connection was not found.");
            return;
        }

        await connectionHandler.CurrentMap.BroadcastPacket(new TalkPlayerServerPacket
        {
            Message = packet.Message,
            PlayerId = connectionHandler.SessionId
        }, except: connectionHandler);
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (TalkReportClientPacket)packet);
    }
}