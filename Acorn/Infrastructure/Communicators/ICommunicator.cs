namespace Acorn.Infrastructure.Communicators;

public interface ICommunicator
{
    Task Send(IEnumerable<byte> bytes);
    Stream Receive();
    void Close();
    string GetConnectionOrigin();
}