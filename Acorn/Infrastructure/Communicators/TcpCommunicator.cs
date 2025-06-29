using System.Net.Sockets;
using Acorn.Extensions;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicator(TcpClient client, ILogger<TcpCommunicator> logger) : ICommunicator
{
    public async Task Send(IEnumerable<byte> bytes)
    {
        await client.GetStream().WriteAsync(bytes.AsReadOnly());
    }

    public Stream Receive() => client.GetStream();
    public void Close()
    {
        client.Close();
    }

    public string GetConnectionOrigin()
        => client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
}