using System.Net.Sockets;
using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Infrastructure.Communicators;

public class TcpCommunicator(TcpClient client, ILogger<TcpCommunicator> logger) : ICommunicator
{
    public async Task Send(IPacket packet, int serverEncryptionMulti)
    {
        logger.LogDebug("[Server] {Packet}", packet.ToString());
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
        var fullBytes = encodedLength[..2].Concat(bytes);
        await client.GetStream().WriteAsync(fullBytes.AsReadOnly());
    }

    public Stream Receive() => client.GetStream();
    public void Close()
    {
        throw new NotImplementedException();
    }

    public string GetConnectionOrigin()
        => client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
}