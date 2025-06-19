using Acorn.Infrastructure;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

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
            return Task.CompletedTask;
        }

        _logger.LogDebug("Got expected connection accept packet from {Location} for player id {PlayerId}",
            playerState.TcpClient.Client.RemoteEndPoint, playerState.SessionId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (ConnectionAcceptClientPacket)packet);
    }
}