using Acorn.Net.Models;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresState(ClientState.Initialized)]
public class ConnectionAcceptClientPacketHandler(
    ILogger<ConnectionAcceptClientPacketHandler> logger
) : IPacketHandler<ConnectionAcceptClientPacket>
{
    private readonly ILogger<ConnectionAcceptClientPacketHandler> _logger = logger;

    public Task HandleAsync(PlayerState playerState,
        ConnectionAcceptClientPacket packet)
    {
        if (playerState.SessionId != packet.PlayerId)
        {
            _logger.LogError(
                "Mismatch PlayerId. Got {Actual} from packet but expected to be {Expected} from server records. Dropping connection.",
                packet.PlayerId, playerState.SessionId);
            playerState.Disconnect();
            return Task.CompletedTask;
        }

        if (playerState.ClientEncryptionMulti != packet.ClientEncryptionMultiple ||
            playerState.ServerEncryptionMulti != packet.ServerEncryptionMultiple)
        {
            _logger.LogError(
                "Mismatch encryption multiples for session {SessionId}. " +
                "Got client={ClientGot}, server={ServerGot} but expected client={ClientExpected}, server={ServerExpected}. Dropping connection.",
                playerState.SessionId,
                packet.ClientEncryptionMultiple, packet.ServerEncryptionMultiple,
                playerState.ClientEncryptionMulti, playerState.ServerEncryptionMulti);
            playerState.Disconnect();
            return Task.CompletedTask;
        }

        _logger.LogDebug("Got expected connection accept packet from {Location} for player id {PlayerId}",
            playerState.Communicator.GetConnectionOrigin(), playerState.SessionId);
        playerState.ClientState = ClientState.Accepted;
        return Task.CompletedTask;
    }

}
