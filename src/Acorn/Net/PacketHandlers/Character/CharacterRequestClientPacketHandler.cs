using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterRequestClientPacketHandler : IPacketHandler<CharacterRequestClientPacket>
{
    public async Task HandleAsync(PlayerState playerState,
        CharacterRequestClientPacket packet)
    {
        if (string.Equals(packet.RequestString, "new", StringComparison.OrdinalIgnoreCase) is false)
        {
        }

        if (playerState.Account?.Characters.Count() >= 3)
        {
            await playerState.Send(new CharacterReplyServerPacket
            {
                ReplyCode = CharacterReply.Full,
                ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataFull()
            });
        }

        await playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = (CharacterReply)playerState.SessionId,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataDefault()
        });
    }

}