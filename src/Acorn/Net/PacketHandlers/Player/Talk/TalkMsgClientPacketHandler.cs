using Acorn.Game.Services;
using Acorn.Net.PacketHandlers;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
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
            playerState.Character?.Name ?? "Unknown", DateTime.UtcNow, _world.NextGlobalMessageSequence());
        _world.AddGlobalMessage(globalMessage);

        // The sender's client echoes their own global message locally, so advance
        // their cursor too and never replay their own message back to them.
        playerState.LastGlobalMessageSequence = globalMessage.Sequence;

        var recipients = _world.GetGlobalChatListeners()
            .Where(x => x != playerState)
            .ToList();

        await Task.WhenAll(recipients.Select(x => x.Send(new TalkMsgServerPacket
        {
            Message = message,
            PlayerName = playerState.Character?.Name!
        })));

        foreach (var recipient in recipients)
        {
            recipient.LastGlobalMessageSequence = globalMessage.Sequence;
        }
    }
}
