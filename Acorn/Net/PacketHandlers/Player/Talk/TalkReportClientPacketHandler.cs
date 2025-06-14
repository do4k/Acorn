using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkReportClientPacketHandler(WorldState world, IEnumerable<ITalkHandler> talkHandlers, ILogger<TalkReportClientPacketHandler> logger)
    : IPacketHandler<TalkReportClientPacket>
{
    public async Task HandleAsync(PlayerConnection playerConnection,
        TalkReportClientPacket packet)
    {
        var author = playerConnection.Character;

        if (author?.Admin > AdminLevel.Player && packet.Message.StartsWith("$"))
        {
            var args = packet.Message.Split(" ");
            var command = args[0][1..];

            var handler = talkHandlers.FirstOrDefault(x => x.CanHandle(command));
            if (handler is null)
            {
                return;
            }

            await handler.HandleAsync(playerConnection, command, args[1..]);
            return;
        }

        var map = world.MapFor(playerConnection);
        if (map is null)
        {
            logger.LogError("Tried to handle talk report packet, but the map for the player connection was not found.");
            return;
        }

        await map.BroadcastPacket(new TalkPlayerServerPacket
        {
            Message = packet.Message,
            PlayerId = playerConnection.SessionId
        }, playerConnection);
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (TalkReportClientPacket)packet);
    }
}