using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Communicators;

/// <summary>
/// Adapts a WebSocket into a Stream for reading. A single instance is created per
/// WebSocketCommunicator and reused for the lifetime of the connection, preserving
/// any buffered data between read calls.
/// </summary>
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
        // PlayerState.Listen() uses ReadAsync/ReadExactlyAsync exclusively.
        // Throwing here ensures we catch any accidental sync usage rather than
        // silently deadlocking via .GetAwaiter().GetResult().
        throw new NotSupportedException(
            "Synchronous Read is not supported on WebSocketStream. Use ReadAsync instead.");
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
