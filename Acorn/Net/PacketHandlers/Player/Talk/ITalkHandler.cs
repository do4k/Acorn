namespace Acorn.Net.PacketHandlers.Player.Talk;

public interface ITalkHandler
{
    bool CanHandle(string command);
    Task HandleAsync(PlayerState playerState, string command, params string[] args);
}