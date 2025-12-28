﻿using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkReportClientPacketHandler(
    IEnumerable<ITalkHandler> talkHandlers,
    WiseManTalkHandler wiseManHandler,
    ILogger<TalkReportClientPacketHandler> logger)
    : IPacketHandler<TalkReportClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        TalkReportClientPacket packet)
    {
        var author = playerState.Character;

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

        // Check if the message is directed at the Wise Man NPC
        wiseManHandler.TryHandleMessage(playerState, packet.Message);

        if (playerState.CurrentMap is null)
        {
            logger.LogError("Tried to handle talk report packet, but the map for the player connection was not found.");
            return;
        }

        await playerState.CurrentMap.BroadcastPacket(new TalkPlayerServerPacket
        {
            Message = packet.Message,
            PlayerId = playerState.SessionId
        }, except: playerState);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (TalkReportClientPacket)packet);
    }
}