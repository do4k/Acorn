using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.World.Services.Map;
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
    WiseManTalkHandler wiseManHandler,
    IMapTileService tileService,
    IChatSanitizer chatSanitizer)
    : IPacketHandler<TalkReportClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        TalkReportClientPacket packet)
    {
        // Muted players cannot chat, run commands, or talk to the Wise Man.
        if (playerState.IsMuted)
        {
            return;
        }

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

        var message = chatSanitizer.Sanitize(packet.Message, author!.Name);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Local chat is only visible to players within client render range.
        var origin = author.AsCoords();
        var recipients = playerState.CurrentMap!.Players.Values
            .Where(p => p.SessionId != playerState.SessionId
                        && p.Character is not null
                        && tileService.InClientRange(origin, p.Character!.AsCoords()));

        await Task.WhenAll(recipients.Select(p => p.Send(new TalkPlayerServerPacket
        {
            Message = message,
            PlayerId = playerState.SessionId
        })));
    }
}
