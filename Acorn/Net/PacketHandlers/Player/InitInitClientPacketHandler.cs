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

    public async Task HandleAsync(PlayerConnection playerConnection, InitInitClientPacket packet)
    {
        playerConnection.PacketSequencer =
            playerConnection.PacketSequencer.WithSequenceStart(playerConnection.StartSequence);
        playerConnection.ClientEncryptionMulti = playerConnection.Rnd.Next(7) + 6;
        playerConnection.ServerEncryptionMulti = playerConnection.Rnd.Next(7) + 6;

        _logger.LogDebug("Sending Init Server Packet with Seq 1: {Seq1}, Seq 2: {Seq2} PlayerId: {PlayerId}",
            playerConnection.StartSequence.Seq1, playerConnection.StartSequence.Seq2, playerConnection.SessionId);
        await playerConnection.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.Ok,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataOk
            {
                Seq1 = playerConnection.StartSequence.Seq1,
                Seq2 = playerConnection.StartSequence.Seq2,
                ClientEncryptionMultiple = playerConnection.ClientEncryptionMulti,
                ServerEncryptionMultiple = playerConnection.ServerEncryptionMulti,
                PlayerId = playerConnection.SessionId,
                ChallengeResponse = ServerVerifier.Hash(packet.Challenge)
            }
        });

        playerConnection.ClientState = ClientState.Initialized;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (InitInitClientPacket)packet);
    }
}