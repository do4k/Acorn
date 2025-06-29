using Acorn.Infrastructure;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class ConnectionAcceptClientPacketHandler(
    ILogger<ConnectionAcceptClientPacketHandler> logger
) : IPacketHandler<ConnectionAcceptClientPacket>
{
    private readonly ILogger<ConnectionAcceptClientPacketHandler> _logger = logger;

    public Task HandleAsync(ConnectionHandler connectionHandler,
        ConnectionAcceptClientPacket packet)
    {
        if (connectionHandler.SessionId != packet.PlayerId)
        {
            _logger.LogError(
                "Mismatch PlayerId. Got {Actual} from packet but expected to be {Expected} from server records. Dropping connection.",
                packet.PlayerId, connectionHandler.SessionId);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Got expected connection accept packet from {Location} for player id {PlayerId}",
            connectionHandler.TcpClient.Client.RemoteEndPoint, connectionHandler.SessionId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (ConnectionAcceptClientPacket)packet);
    }
}