using Acorn.Infrastructure;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class ConnectionAcceptClientPacketHandler(
    ILogger<ConnectionAcceptClientPacketHandler> logger,
    ISessionGenerator sessionGenerator
) : IPacketHandler<ConnectionAcceptClientPacket>
{
    private readonly ILogger<ConnectionAcceptClientPacketHandler> _logger = logger;
    private readonly ISessionGenerator _sessionGenerator = sessionGenerator;

    public Task HandleAsync(PlayerConnection playerConnection,
        ConnectionAcceptClientPacket packet)
    {
        if (playerConnection.SessionId != packet.PlayerId)
        {
            _logger.LogError(
                "Mismatch PlayerId. Got {Actual} from packet but expected to be {Expected} from server records. Dropping connection.",
                packet.PlayerId, playerConnection.SessionId);
            return Task.CompletedTask;
        }

        //playerConnection.SessionId = _sessionGenerator.Generate();
        _logger.LogDebug("Got expected connection accept packet from {Location} for player id {PlayerId}",
            playerConnection.TcpClient.Client.RemoteEndPoint, playerConnection.SessionId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (ConnectionAcceptClientPacket)packet);
    }
}