using System.Net;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class WebSocketCommunicatorFactory(ILogger<WebSocketCommunicator> logger)
{
    public Task<WebSocketCommunicator> InitialiseAsync(HttpListenerContext client, CancellationToken cancellationToken = default)
        => WebSocketCommunicator.CreateAsync(client, logger, cancellationToken);
}