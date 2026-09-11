using Acorn.Game.Services;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
internal class TalkTellClientPacketHandler(IWorldQueries world, IChatSanitizer chatSanitizer)
    : IPacketHandler<TalkTellClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkTellClientPacket packet)
    {
        // Muted players cannot whisper.
        if (playerState.IsMuted)
        {
            return;
        }

        var target = world.FindPlayerByName(packet.Name);

        // Hidden admins and unknown players are indistinguishable to the sender.
        if (target?.Character is null || target.Character.Hidden || !target.Whispers)
        {
            await playerState.Send(new TalkReplyServerPacket
            {
                ReplyCode = TalkReply.NotFound,
                Name = packet.Name
            });
            return;
        }

        var message = chatSanitizer.Sanitize(packet.Message, playerState.Character!.Name);

        await target.Send(new TalkTellServerPacket
        {
            Message = message,
            PlayerName = playerState.Character.Name!
        });
    }
}
