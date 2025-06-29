using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Infrastructure.Communicators;

public class WebSocketCommunicator : ICommunicator
{
    private readonly HttpListenerWebSocketContext _wsContext;
    private readonly WebSocket _webSocket;
    private readonly ILogger<WebSocketCommunicator> _logger;

    public WebSocketCommunicator(HttpListenerContext context, ILogger<WebSocketCommunicator> logger)
    {
        _wsContext = context.AcceptWebSocketAsync(null).GetAwaiter().GetResult();
        _webSocket = _wsContext.WebSocket;
        _logger = logger;
    }

    public async Task Send(IPacket packet, int serverEncryptionMulti)
    {
        _logger.LogDebug("[Server] {Packet}", packet.ToString());
        var writer = new EoWriter();
        writer.AddByte((int)packet.Action);
        writer.AddByte((int)packet.Family);
        packet.Serialize(writer);
        var bytes = packet switch
        {
            InitInitServerPacket _ => writer.ToByteArray(),
            _ => DataEncrypter.FlipMSB(
                DataEncrypter.Interleave(DataEncrypter.SwapMultiples(writer.ToByteArray(), serverEncryptionMulti)))
        };

        var encodedLength = NumberEncoder.EncodeNumber(bytes.Length);
        var fullBytes = encodedLength[..2].Concat(bytes).ToArray();
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(fullBytes),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);
    }

    public Stream Receive()
    {
        // Return a stream that reads from the WebSocket
        return new WebSocketStream(_webSocket);
    }

    public void Close()
    {
        if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
        {
            _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public string GetConnectionOrigin()
    {
        return _wsContext.Origin;
    }
}

// Helper stream to read from WebSocket
public class WebSocketStream : Stream
{
    private readonly WebSocket _webSocket;
    private readonly MemoryStream _buffer = new();

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
        if (_buffer.Length != 0 && _buffer.Position != _buffer.Length)
        {
            return _buffer.Read(buffer, offset, count);
        }

        var receiveBuffer = new byte[4096];
        var result = _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).GetAwaiter()
            .GetResult();
        _buffer.SetLength(0);
        _buffer.Position = 0;
        _buffer.Write(receiveBuffer, 0, result.Count);
        _buffer.Position = 0;
        return _buffer.Read(buffer, offset, count);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
