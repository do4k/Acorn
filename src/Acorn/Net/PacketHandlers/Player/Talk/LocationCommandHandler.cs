using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     #loc / #location - Shows the player's current map and coordinates.
/// </summary>
public class LocationCommandHandler(INotificationService notifications) : IPlayerCommandHandler
{
    public bool CanHandle(string command)
        => command.Equals("loc", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("location", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var character = playerState.Character;
        if (character is null) return;

        var mapName = playerState.CurrentMap?.Data?.Name ?? "Unknown";
        var message = $"Map: {character.Map} ({mapName}) X: {character.X} Y: {character.Y}";
        await notifications.SystemMessage(playerState, message);
    }
}
