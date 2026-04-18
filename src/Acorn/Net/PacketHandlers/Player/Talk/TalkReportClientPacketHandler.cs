using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
internal class TalkReportClientPacketHandler(
    IEnumerable<ITalkHandler> talkHandlers,
    IEnumerable<IPlayerCommandHandler> playerCommandHandlers,
    WiseManTalkHandler wiseManHandler)
    : IPacketHandler<TalkReportClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        TalkReportClientPacket packet)
    {
        var author = playerState.Character!;

        if (author?.Admin > AdminLevel.Player && packet.Message.StartsWith("$"))
        {
            var args = packet.Message.Split(" ");
            var command = args[0][1..];

            var handler = talkHandlers.FirstOrDefault(x => x.CanHandle(command));
            if (handler is null)
            {
                return;
            }

            await handler.HandleAsync(playerState, command, args[1..]);
            return;
        }

        // Handle player # commands (available to all players)
        if (packet.Message.StartsWith('#'))
        {
            var args = packet.Message[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length > 0)
            {
                var command = args[0];
                var handler = playerCommandHandlers.FirstOrDefault(x => x.CanHandle(command));
                if (handler is not null)
                {
                    await handler.HandleAsync(playerState, command, args[1..]);
                    return;
                }
            }
            // If no handler found, fall through to normal chat
        }

        // Check if the message is directed at the Wise Man NPC
        wiseManHandler.TryHandleMessage(playerState, packet.Message);

        // Muted players cannot chat
        if (playerState.IsMuted)
        {
            return;
        }

        await playerState.CurrentMap!.BroadcastPacket(new TalkPlayerServerPacket
        {
            Message = packet.Message,
            PlayerId = playerState.SessionId
        }, playerState);
    }

}