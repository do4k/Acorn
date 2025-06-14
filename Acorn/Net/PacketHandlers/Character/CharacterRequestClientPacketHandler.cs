using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterRequestClientPacketHandler : IPacketHandler<CharacterRequestClientPacket>
{
    public async Task HandleAsync(PlayerConnection playerConnection,
        CharacterRequestClientPacket packet)
    {
        if (string.Equals(packet.RequestString, "new", StringComparison.OrdinalIgnoreCase) is false)
        {
        
        }

        if (playerConnection.Account?.Characters.Count() >= 3)
        {
            await playerConnection.Send(new CharacterReplyServerPacket
            {
                ReplyCode = CharacterReply.Full,
                ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataFull()
            });
        }

        await playerConnection.Send(new CharacterReplyServerPacket
        {
            ReplyCode = (CharacterReply)playerConnection.SessionId,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataDefault()
        });
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (CharacterRequestClientPacket)packet);
    }
}