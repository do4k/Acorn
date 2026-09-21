using System.Net.Sockets;
using Acorn.Extensions;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicator(TcpClient client) : ICommunicator
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public async Task Send(IEnumerable<byte> bytes)
    {
        if (_disposed)
        {
            throw new ConnectionClosedException("Cannot send data - client is not connected");
        }

        // The connection may be closed while we are waiting for the lock. The lock itself
        // is never disposed (see CloseAsync), so a guard before and after acquiring it
        // keeps this safe without racing on a disposed synchronisation primitive.
        await _sendLock.WaitAsync();
        try
        {
            if (!IsConnected)
            {
                throw new ConnectionClosedException("Cannot send data - client is not connected");
            }

            await client.GetStream().WriteAsync(bytes.AsReadOnly());
        }
        catch (ObjectDisposedException ex)
        {
            _disposed = true;
            throw new ConnectionClosedException("Cannot send data - client is not connected", ex);
        }
        catch (IOException ex)
        {
            _disposed = true;
            throw new ConnectionClosedException("Cannot send data - connection failed", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public Stream Receive()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Cannot receive data - client is not connected");
        }

        return client.GetStream();
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

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

            // _sendLock is deliberately not disposed. A broadcast can be waiting on it
            // for this player at the exact moment they disconnect, and disposing the
            // semaphore under that waiter turns an ordinary disconnect into an
            // ObjectDisposedException that would abort the broadcast (and, before this
            // fix, disconnect whoever was broadcasting).
        }
    }

    public string GetConnectionOrigin()
    {
        return client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
    }

    public bool IsConnected => !_disposed && client.Connected && client.Client.Connected;
}