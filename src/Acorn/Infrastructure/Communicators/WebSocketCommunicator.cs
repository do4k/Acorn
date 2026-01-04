using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

public class WebSocketCommunicator : ICommunicator
{
    private readonly ILogger<WebSocketCommunicator> _logger;
    private readonly WebSocket _webSocket;
    private readonly HttpListenerWebSocketContext _wsContext;
    private bool _disposed;

    private WebSocketCommunicator(HttpListenerWebSocketContext wsContext, ILogger<WebSocketCommunicator> logger)
    {
        _wsContext = wsContext;
        _webSocket = wsContext.WebSocket;
        _logger = logger;
    }

    public async Task Send(IEnumerable<byte> bytes)
    {
        try
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot send data - WebSocket is not connected");
            }

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes.ToArray()),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None);
        }
        catch (WebSocketException)
        {
            _disposed = true;
            throw;
        }
    }

    public Stream Receive()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Cannot receive data - WebSocket is not connected");
        }

        return new WebSocketStream(_webSocket);
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

// Helper stream to read from WebSocket
public class WebSocketStream : Stream
{
    private readonly MemoryStream _buffer = new();
    private readonly byte[] _receiveBuffer = new byte[8192]; // Match TCP buffer size
    private readonly WebSocket _webSocket;

    public WebSocketStream(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _buffer.Length;

    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Return buffered data if available
        if (_buffer.Length > 0 && _buffer.Position < _buffer.Length)
        {
            return _buffer.Read(buffer, offset, count);
        }

        // Receive new data from WebSocket
        try
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(_receiveBuffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return 0; // End of stream
            }

            // Reset buffer and write new data
            _buffer.SetLength(0);
            _buffer.Position = 0;
            _buffer.Write(_receiveBuffer, 0, result.Count);
            _buffer.Position = 0;

            return _buffer.Read(buffer, offset, count);
        }
        catch (WebSocketException)
        {
            return 0; // Connection closed or error
        }
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}