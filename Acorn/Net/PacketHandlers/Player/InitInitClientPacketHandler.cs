using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class InitInitClientPacketHandler(ILogger<InitInitClientPacketHandler> logger)
    : IPacketHandler<InitInitClientPacket>
{
    private readonly ILogger<InitInitClientPacketHandler> _logger = logger;

    public async Task HandleAsync(PlayerState playerState, InitInitClientPacket packet)
    {
        playerState.PacketSequencer =
            playerState.PacketSequencer.WithSequenceStart(playerState.StartSequence);
        playerState.ClientEncryptionMulti = playerState.Rnd.Next(7) + 6;
        playerState.ServerEncryptionMulti = playerState.Rnd.Next(7) + 6;

        _logger.LogDebug("Sending Init Server Packet with Seq 1: {Seq1}, Seq 2: {Seq2} PlayerId: {PlayerId}",
            playerState.StartSequence.Seq1, playerState.StartSequence.Seq2, playerState.SessionId);
        await playerState.Send(new InitInitServerPacket
        {
            ReplyCode = InitReply.Ok,
            ReplyCodeData = new InitInitServerPacket.ReplyCodeDataOk
            {
                Seq1 = playerState.StartSequence.Seq1,
                Seq2 = playerState.StartSequence.Seq2,
                ClientEncryptionMultiple = playerState.ClientEncryptionMulti,
                ServerEncryptionMultiple = playerState.ServerEncryptionMulti,
                PlayerId = playerState.SessionId,
                ChallengeResponse = ServerVerifier.Hash(packet.Challenge)
            }
        });

        playerState.ClientState = ClientState.Initialized;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (InitInitClientPacket)packet);
    }
}