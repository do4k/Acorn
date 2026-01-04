using Acorn.Infrastructure.Communicators;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class PlayerStateFactory(
    IEnumerable<IPacketHandler> packetHandlers,
    ILogger<PlayerState> logger,
    IOptions<ServerOptions> serverOptions)
{
    public PlayerState CreatePlayerState(ICommunicator communicator, int sessionId, Action<PlayerState> onDispose)
    {
        return new PlayerState(packetHandlers, communicator, logger, serverOptions, sessionId, onDispose);
    }
}