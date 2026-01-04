namespace Acorn.Infrastructure.Communicators;

public interface ICommunicator
{
    bool IsConnected { get; }
    Task Send(IEnumerable<byte> bytes);
    Stream Receive();
    Task CloseAsync(CancellationToken cancellationToken = default);
    string GetConnectionOrigin();
}