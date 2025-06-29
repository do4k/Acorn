namespace Acorn.Net.PacketHandlers.Player.Talk;

public interface ITalkHandler
{
    bool CanHandle(string command);
    Task HandleAsync(ConnectionHandler connectionHandler, string command, params string[] args);
}