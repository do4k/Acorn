using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Infrastructure.Communicators;

public interface ICommunicator
{
    Task Send(IPacket packet, int serverEncryptionMulti);
    Stream Receive();
    void Close();
    string GetConnectionOrigin();
}