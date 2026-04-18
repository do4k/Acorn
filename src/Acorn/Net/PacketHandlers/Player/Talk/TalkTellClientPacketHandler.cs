using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player.Talk;

[RequiresCharacter]
internal class TalkTellClientPacketHandler(IWorldQueries world) : IPacketHandler<TalkTellClientPacket>
{
    public async Task HandleAsync(PlayerState playerState, TalkTellClientPacket packet)
    {
        var target = world.FindPlayerByName(packet.Name);
        if (target is null)
        {
            await playerState.Send(new TalkReplyServerPacket
            {
                ReplyCode = TalkReply.NotFound,
                Name = packet.Name
            });
            return;
        }

        await target.Send(new TalkTellServerPacket
        {
            Message = packet.Message,
            PlayerName = playerState.Character!.Name!
        });
    }
}
