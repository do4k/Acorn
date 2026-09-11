using Acorn.Game.Services;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkMsgClientPacketHandler : IPacketHandler<TalkMsgClientPacket>
{
    private readonly IChatSanitizer _chatSanitizer;
    private readonly IWorldQueries _world;

    public TalkMsgClientPacketHandler(IWorldQueries world, IChatSanitizer chatSanitizer)
    {
        _world = world;
        _chatSanitizer = chatSanitizer;
    }

    public async Task HandleAsync(PlayerState playerState, TalkMsgClientPacket packet)
    {
        // Muted players cannot use global chat.
        if (playerState.IsMuted)
        {
            return;
        }

        // Jailed players cannot use global chat.
        if (playerState.IsJailed)
        {
            return;
        }

        var message = _chatSanitizer.Sanitize(packet.Message, playerState.Character?.Name);

        var globalMessage = new GlobalMessage(Guid.NewGuid(), message,
            playerState.Character?.Name ?? "Unknown", DateTime.UtcNow);
        _world.AddGlobalMessage(globalMessage);

        var broadcast = _world.GetGlobalChatListeners()
            .Where(x => x != playerState)
            .Select(x => x.Send(new TalkMsgServerPacket
            {
                Message = message,
                PlayerName = playerState.Character?.Name!
            }));

        await Task.WhenAll(broadcast);
    }
}
