using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterRequestClientPacketHandler : IPacketHandler<CharacterRequestClientPacket>
{
    public async Task HandleAsync(ConnectionHandler connectionHandler,
        CharacterRequestClientPacket packet)
    {
        if (string.Equals(packet.RequestString, "new", StringComparison.OrdinalIgnoreCase) is false)
        {

        }

        if (connectionHandler.Account?.Characters.Count() >= 3)
        {
            await connectionHandler.Send(new CharacterReplyServerPacket
            {
                ReplyCode = CharacterReply.Full,
                ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataFull()
            });
        }

        await connectionHandler.Send(new CharacterReplyServerPacket
        {
            ReplyCode = (CharacterReply)connectionHandler.SessionId,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataDefault()
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (CharacterRequestClientPacket)packet);
    }
}