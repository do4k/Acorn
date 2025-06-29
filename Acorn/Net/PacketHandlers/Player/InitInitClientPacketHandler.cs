using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class InitInitClientPacketHandler(ILogger<InitInitClientPacketHandler> logger)
    : IPacketHandler<InitInitClientPacket>
{
    private readonly ILogger<InitInitClientPacketHandler> _logger = logger;

    public async Task HandleAsync(ConnectionHandler connectionHandler, InitInitClientPacket packet)
    {
        connectionHandler.PacketSequencer =
            connectionHandler.PacketSequencer.WithSequenceStart(connectionHandler.StartSequence);
        connectionHandler.ClientEncryptionMulti = connectionHandler.Rnd.Next(7) + 6;
        connectionHandler.ServerEncryptionMulti = connectionHandler.Rnd.Next(7) + 6;

        _logger.LogDebug("Sending Init Server Packet with Seq 1: {Seq1}, Seq 2: {Seq2} PlayerId: {PlayerId}",
            connectionHandler.StartSequence.Seq1, connectionHandler.StartSequence.Seq2, connectionHandler.SessionId);
        await connectionHandler.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.Ok,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataOk
            {
                Seq1 = connectionHandler.StartSequence.Seq1,
                Seq2 = connectionHandler.StartSequence.Seq2,
                ClientEncryptionMultiple = connectionHandler.ClientEncryptionMulti,
                ServerEncryptionMultiple = connectionHandler.ServerEncryptionMulti,
                PlayerId = connectionHandler.SessionId,
                ChallengeResponse = ServerVerifier.Hash(packet.Challenge)
            }
        });

        connectionHandler.ClientState = ClientState.Initialized;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (InitInitClientPacket)packet);
    }
}