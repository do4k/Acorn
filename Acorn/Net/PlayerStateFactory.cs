using Acorn.Infrastructure.Communicators;
using Acorn.Net.PacketHandlers;
using Microsoft.Extensions.Logging;

namespace Acorn.Net;

public class PlayerStateFactory(IEnumerable<IPacketHandler> packetHandlers, ILogger<PlayerState> logger)
{
    public PlayerState CreatePlayerState(ICommunicator communicator, int sessionId, Action<PlayerState> onDispose)
        => new PlayerState(packetHandlers, communicator, logger, sessionId, onDispose);
}