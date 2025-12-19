namespace Acorn.Infrastructure.Communicators;

public interface ICommunicator
{
    Task Send(IEnumerable<byte> bytes);
    Stream Receive();
    Task CloseAsync(CancellationToken cancellationToken = default);
    string GetConnectionOrigin();
    bool IsConnected { get; }
}