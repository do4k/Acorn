using Acorn.Options;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Character;

internal class CharacterRequestClientPacketHandler(
    IOptions<ServerOptions> serverOptions) : IPacketHandler<CharacterRequestClientPacket>
{
    private readonly ServerOptions _serverOptions = serverOptions.Value;

    public async Task HandleAsync(PlayerState playerState,
        CharacterRequestClientPacket packet)
    {
        if (playerState.Account?.Characters.Count >= _serverOptions.MaxCharacters)
        {
            await playerState.Send(new CharacterReplyServerPacket
            {
                ReplyCode = CharacterReply.Full,
                ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataFull()
            });
            return;
        }

        await playerState.Send(new CharacterReplyServerPacket
        {
            ReplyCode = (CharacterReply)playerState.SessionId,
            ReplyCodeData = new CharacterReplyServerPacket.ReplyCodeDataDefault()
        });
    }

}
