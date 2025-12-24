using System.Net.Sockets;
using Acorn.Extensions;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicator(TcpClient client) : ICommunicator
{
    private bool _disposed;

    public async Task Send(IEnumerable<byte> bytes)
    {
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cannot send data - client is not connected");

            await client.GetStream().WriteAsync(bytes.AsReadOnly());
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            _disposed = true;
            throw;
        }
    }

    public Stream Receive()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Cannot receive data - client is not connected");

        return client.GetStream();
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (client.Connected)
            {
                await client.GetStream().FlushAsync(cancellationToken);
            }
        }
        catch
        {
            // Ignore errors during close
        }
        finally
        {
            client.Close();
        }
    }

    public string GetConnectionOrigin()
        => client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

    public bool IsConnected => !_disposed && client.Connected && client.Client.Connected;
}