namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Interface for player # commands (available to all players, not just admins).
/// </summary>
public interface IPlayerCommandHandler
{
    bool CanHandle(string command);
    Task HandleAsync(PlayerState playerState, string command, params string[] args);
}
