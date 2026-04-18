using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Tests.Integration;

/// <summary>
/// A test client that speaks the EO protocol over TCP or WebSocket.
/// Handles framing (2-byte length prefix), encryption, and packet sequencing
/// so integration tests can focus on the logical flow.
/// </summary>
public sealed class EoTestClient : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private readonly TcpClient? _tcp;
    private readonly ClientWebSocket? _ws;
    private readonly Stream? _tcpStream;
    private readonly PacketResolver _resolver = new("Moffat.EndlessOnline.SDK.Protocol.Net.Server");

    // EO protocol state — updated during the init handshake
    private PacketSequencer _sequencer = new(ZeroSequence.Instance);
    private int _clientEncryptionMulti;
    private int _serverEncryptionMulti;

    public int PlayerId { get; private set; }

    // --- Construction / Factory ---

    private EoTestClient(TcpClient tcp)
    {
        _tcp = tcp;
        _tcpStream = tcp.GetStream();
    }

    private EoTestClient(ClientWebSocket ws)
    {
        _ws = ws;
    }

    public static async Task<EoTestClient> ConnectTcpAsync(int port)
    {
        var tcp = new TcpClient { NoDelay = true };
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        return new EoTestClient(tcp);
    }

    public static async Task<EoTestClient> ConnectWebSocketAsync(int port, CancellationToken ct = default)
    {
        var ws = new ClientWebSocket();
        ws.Options.AddSubProtocol("binary");
        await ws.ConnectAsync(new Uri($"ws://localhost:{port}/"), ct);
        return new EoTestClient(ws);
    }

    // --- Low-level send/receive ---

    /// <summary>
    /// Sends a packet with proper EO protocol framing:
    /// [2-byte encoded length][action][family][sequence?][encrypted payload]
    /// </summary>
    public async Task SendPacketAsync(IPacket packet)
    {
        var writer = new EoWriter();
        writer.AddByte((int)packet.Action);
        writer.AddByte((int)packet.Family);

        // Sequence handling — must mirror server's HandleSequence logic
        var isInitInit = packet.Family == PacketFamily.Init && packet.Action == PacketAction.Init;
        var isInitFamily = packet.Family == PacketFamily.Init;

        if (isInitInit)
        {
            // Init.Init has no sequence bytes; advance sequencer to stay in sync
            _sequencer.NextSequence();
        }
        else if (isInitFamily)
        {
            // Other Init-family packets have a 1-byte char sequence
            var seq = _sequencer.NextSequence();
            writer.AddChar(seq);
        }
        else
        {
            // Normal packets: char if < CHAR_MAX, short otherwise
            var seq = _sequencer.NextSequence();
            if (seq >= (int)EoNumericLimits.CHAR_MAX)
            {
                writer.AddShort(seq);
            }
            else
            {
                writer.AddChar(seq);
            }
        }

        packet.Serialize(writer);

        // Encrypt (skip when encryption hasn't been established yet)
        var bytes = _clientEncryptionMulti switch
        {
            0 => writer.ToByteArray(),
            _ => DataEncrypter.FlipMSB(
                DataEncrypter.Interleave(
                    DataEncrypter.SwapMultiples(writer.ToByteArray(), _clientEncryptionMulti)))
        };

        // Frame with 2-byte encoded length prefix
        var encodedLength = NumberEncoder.EncodeNumber(bytes.Length);
        var frame = encodedLength[..2].Concat(bytes).ToArray();

        await SendRawAsync(frame);
    }

    /// <summary>
    /// Receives and deserializes a server packet, handling decryption.
    /// </summary>
    public async Task<IPacket> ReceivePacketAsync()
    {
        using var cts = new CancellationTokenSource(ReceiveTimeout);

        // Read 2-byte length prefix
        var lenBytes = await ReceiveBytesAsync(2, cts.Token);
        var length = NumberEncoder.DecodeNumber(lenBytes);

        if (length <= 0 || length > 65535)
        {
            throw new InvalidOperationException($"Invalid packet length: {length}");
        }

        // Read payload
        var payload = await ReceiveBytesAsync(length, cts.Token);

        // Decrypt (skip for pre-init packets where serverMulti is 0)
        var decrypted = _serverEncryptionMulti switch
        {
            0 => payload,
            _ => DataEncrypter.SwapMultiples(
                DataEncrypter.Deinterleave(
                    DataEncrypter.FlipMSB(payload)),
                _serverEncryptionMulti)
        };

        // Deserialize
        var reader = new EoReader(decrypted);
        var action = (PacketAction)reader.GetByte();
        var family = (PacketFamily)reader.GetByte();

        var dataReader = reader.Slice();
        var packet = _resolver.Create(family, action);
        packet.Deserialize(dataReader);

        return packet;
    }

    // --- High-level protocol methods ---

    /// <summary>
    /// Performs the Init handshake: sends InitInitClientPacket, receives InitInitServerPacket,
    /// and configures encryption + sequencing for all subsequent packets.
    /// </summary>
    public async Task<InitInitServerPacket.ReplyCodeDataOk> InitAsync()
    {
        await SendPacketAsync(new InitInitClientPacket
        {
            Challenge = 12345,
            Version = new Moffat.EndlessOnline.SDK.Protocol.Net.Version { Major = 0, Minor = 0, Patch = 28 },
            Hdid = "integration-test"
        });

        var response = await ReceivePacketAsync();
        var initPacket = (InitInitServerPacket)response;
        var data = (InitInitServerPacket.ReplyCodeDataOk)initPacket.ReplyCodeData;

        // Store encryption state
        PlayerId = data.PlayerId;
        _clientEncryptionMulti = data.ClientEncryptionMultiple;
        _serverEncryptionMulti = data.ServerEncryptionMultiple;

        // Switch to the init sequence (mirrors server's handler)
        _sequencer = _sequencer.WithSequenceStart(
            InitSequenceStart.FromInitValues(data.Seq1, data.Seq2));

        return data;
    }

    /// <summary>
    /// Sends ConnectionAcceptClientPacket. The server validates PlayerId but sends no response.
    /// </summary>
    public async Task SendConnectionAcceptAsync()
    {
        await SendPacketAsync(new ConnectionAcceptClientPacket
        {
            ClientEncryptionMultiple = _clientEncryptionMulti,
            ServerEncryptionMultiple = _serverEncryptionMulti,
            PlayerId = PlayerId
        });
        // No response from server — it just logs and continues
    }

    /// <summary>
    /// Sends AccountRequestClientPacket (username availability check).
    /// Returns the session ID from the server's reply (encoded in ReplyCode).
    /// </summary>
    public async Task<int> AccountRequestAsync(string username)
    {
        await SendPacketAsync(new AccountRequestClientPacket
        {
            Username = username
        });

        var response = await ReceivePacketAsync();
        var reply = (AccountReplyServerPacket)response;

        // Server sends the session ID as the ReplyCode (cast to AccountReply enum).
        // For non-existing accounts, this is the session ID; for existing ones, it's AccountReply.Exists.
        return (int)reply.ReplyCode;
    }

    /// <summary>
    /// Sends AccountCreateClientPacket and returns the reply code.
    /// </summary>
    public async Task<AccountReply> AccountCreateAsync(string username, string password, int sessionId)
    {
        await SendPacketAsync(new AccountCreateClientPacket
        {
            SessionId = sessionId,
            Username = username,
            Password = password,
            FullName = "Integration Test",
            Location = "TestLand",
            Email = "test@acorn.local",
            Computer = "TestPC",
            Hdid = "integration-test"
        });

        var response = await ReceivePacketAsync();
        var reply = (AccountReplyServerPacket)response;
        return reply.ReplyCode;
    }

    /// <summary>
    /// Sends LoginRequestClientPacket and returns the full reply.
    /// </summary>
    public async Task<LoginReplyServerPacket> LoginAsync(string username, string password)
    {
        await SendPacketAsync(new LoginRequestClientPacket
        {
            Username = username,
            Password = password
        });

        var response = await ReceivePacketAsync();
        return (LoginReplyServerPacket)response;
    }

    // --- Transport helpers ---

    private async Task SendRawAsync(byte[] data)
    {
        if (_tcpStream is not null)
        {
            await _tcpStream.WriteAsync(data);
            await _tcpStream.FlushAsync();
        }
        else if (_ws is not null)
        {
            await _ws.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }

    // WebSocket buffering — messages may not align with our read boundaries
    private byte[]? _wsBuffer;
    private int _wsBufferOffset;
    private int _wsBufferLength;

    private async Task<byte[]> ReceiveBytesAsync(int count, CancellationToken ct)
    {
        if (_tcpStream is not null)
        {
            var buf = new byte[count];
            await _tcpStream.ReadExactlyAsync(buf, ct);
            return buf;
        }

        // WebSocket path: buffer incoming messages and serve exact byte counts
        _wsBuffer ??= new byte[65536];

        // Compact leftover data to front of buffer
        if (_wsBufferOffset > 0 && _wsBufferLength > 0)
        {
            Buffer.BlockCopy(_wsBuffer, _wsBufferOffset, _wsBuffer, 0, _wsBufferLength);
        }
        _wsBufferOffset = 0;

        while (_wsBufferLength < count)
        {
            var segment = new ArraySegment<byte>(_wsBuffer, _wsBufferLength, _wsBuffer.Length - _wsBufferLength);
            var result = await _ws!.ReceiveAsync(segment, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new EndOfStreamException("WebSocket closed by server");
            }
            _wsBufferLength += result.Count;
        }

        var output = _wsBuffer.AsSpan(_wsBufferOffset, count).ToArray();
        _wsBufferOffset += count;
        _wsBufferLength -= count;
        return output;
    }

    // --- Cleanup ---

    public async ValueTask DisposeAsync()
    {
        if (_tcp is not null)
        {
            _tcp.Close();
            _tcp.Dispose();
        }

        if (_ws is not null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "test done", CancellationToken.None);
                }
                catch
                {
                    // Best effort
                }
            }
            _ws.Dispose();
        }
    }
}
