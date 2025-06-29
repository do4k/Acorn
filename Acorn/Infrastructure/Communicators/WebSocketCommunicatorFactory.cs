using System.Net;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class WebSocketCommunicatorFactory(ILogger<WebSocketCommunicator> logger)
{
    public WebSocketCommunicator Initialise(HttpListenerContext client) => new(client, logger);
}