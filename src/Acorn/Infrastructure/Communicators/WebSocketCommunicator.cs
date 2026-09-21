using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class WebSocketCommunicator : ICommunicator
{
    private readonly ILogger<WebSocketCommunicator> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly WebSocket _webSocket;
    private readonly WebSocketStream _stream;
    private readonly HttpListenerWebSocketContext _wsContext;
    private bool _disposed;

    private WebSocketCommunicator(HttpListenerWebSocketContext wsContext, ILogger<WebSocketCommunicator> logger)
    {
        _wsContext = wsContext;
        _webSocket = wsContext.WebSocket;
        _logger = logger;
        _stream = new WebSocketStream(_webSocket);
    }

    public async Task Send(IEnumerable<byte> bytes)
    {
        if (_disposed)
        {
            throw new ConnectionClosedException("Cannot send data - WebSocket is not connected");
        }

        // The connection may be closed while we are waiting for the lock. The lock itself
        // is never disposed (see CloseAsync), so a guard before and after acquiring it
        // keeps this safe without racing on a disposed synchronisation primitive.
        await _sendLock.WaitAsync();
        try
        {
            if (!IsConnected)
            {
                throw new ConnectionClosedException("Cannot send data - WebSocket is not connected");
            }

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes.ToArray()),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None);
        }
        catch (ObjectDisposedException ex)
        {
            _disposed = true;
            throw new ConnectionClosedException("Cannot send data - WebSocket is not connected", ex);
        }
        catch (InvalidOperationException ex)
        {
            // SendAsync throws InvalidOperationException if the socket is closing or aborted.
            _disposed = true;
            throw new ConnectionClosedException("Cannot send data - WebSocket is not connected", ex);
        }
        catch (WebSocketException ex)
        {
            _disposed = true;
            throw new ConnectionClosedException("Cannot send data - WebSocket connection failed", ex);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Returns the single WebSocketStream instance for this connection.
    /// Unlike the previous implementation, this does NOT create a new stream per call,
    /// so buffered data is preserved between reads.
    /// </summary>
    public Stream Receive()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Cannot receive data - WebSocket is not connected");
        }

        return _stream;
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
            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Error closing WebSocket");
        }
        finally
        {
            _webSocket.Dispose();
            _stream.Dispose();

            // _sendLock is deliberately not disposed. A broadcast can be waiting on it
            // for this player at the exact moment they disconnect, and disposing the
            // semaphore under that waiter turns an ordinary disconnect into an
            // ObjectDisposedException that would abort the broadcast.
        }
    }

    public string GetConnectionOrigin()
    {
        return _wsContext.Origin;
    }

    public bool IsConnected => !_disposed && _webSocket.State == WebSocketState.Open;

    public static async Task<WebSocketCommunicator> CreateAsync(HttpListenerContext context,
        ILogger<WebSocketCommunicator> logger, CancellationToken cancellationToken = default)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        return new WebSocketCommunicator(wsContext, logger);
    }
}
