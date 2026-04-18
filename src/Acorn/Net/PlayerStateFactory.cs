using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class PlayerStateFactory(
    IEnumerable<IPacketHandler> packetHandlers,
    ILogger<PlayerState> logger,
    IOptions<ServerOptions> serverOptions,
    AcornMetrics metrics)
{
    public PlayerState CreatePlayerState(ICommunicator communicator, int sessionId, Func<PlayerState, Task> onDispose)
    {
        return new PlayerState(packetHandlers, communicator, logger, serverOptions, metrics, sessionId, onDispose);
    }
}