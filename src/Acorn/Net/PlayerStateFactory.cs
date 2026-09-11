using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class PlayerStateFactory(
    IServiceScopeFactory scopeFactory,
    ILogger<PlayerState> logger,
    IOptions<ServerOptions> serverOptions,
    AcornMetrics metrics)
{
    public PlayerState CreatePlayerState(ICommunicator communicator, int sessionId, Func<PlayerState, Task> onDispose)
    {
        // Each connection gets its own DI scope so its packet handlers resolve
        // per-connection scoped services (notably the EF Core DbContext) instead of
        // every connection sharing one context from the root provider.
        var scope = scopeFactory.CreateScope();
        var packetHandlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IPacketHandler>>();

        return new PlayerState(packetHandlers, communicator, logger, serverOptions, metrics, sessionId,
            async player =>
            {
                try
                {
                    await onDispose(player);
                }
                finally
                {
                    scope.Dispose();
                }
            });
    }
}
