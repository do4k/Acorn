using System.Net.Sockets;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicatorFactory
{
    public TcpCommunicator Initialise(TcpClient client)
    {
        // Configure socket options for optimal game server performance
        client.NoDelay = true; // Disable Nagle's algorithm for low-latency gaming
        client.ReceiveTimeout = 30000; // 30 second receive timeout
        client.SendTimeout = 30000; // 30 second send timeout
        client.ReceiveBufferSize = 8192; // 8KB receive buffer
        client.SendBufferSize = 8192; // 8KB send buffer

        // Enable TCP keep-alive to detect dead connections
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        return new TcpCommunicator(client);
    }
}