using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicatorFactory(ILogger<TcpCommunicator> logger)
{
    public TcpCommunicator Initialise(TcpClient client) => new(client, logger);
}