using Acorn.Net.Services;
using Acorn.World;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $who / $online - Lists the players currently online with their map and coordinates.
/// </summary>
public class WhoCommandHandler(IWorldQueries world, INotificationService notifications) : ITalkHandler
{
    private const int MaxListed = 50;

    public bool CanHandle(string command)
        => command.Equals("who", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("online", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var players = world.GetAllPlayers()
            .Where(p => p.Character is not null)
            .OrderBy(p => p.Character!.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await notifications.SystemMessage(playerState, $"Online players ({players.Count}):");

        foreach (var player in players.Take(MaxListed))
        {
            var character = player.Character!;
            var mapName = player.CurrentMap?.Data?.Name;
            var location = string.IsNullOrEmpty(mapName) ? $"Map {character.Map}" : $"Map {character.Map} ({mapName})";
            await notifications.SystemMessage(playerState, $"  {character.Name} - {location} @ {character.X},{character.Y}");
        }

        if (players.Count > MaxListed)
        {
            await notifications.SystemMessage(playerState, $"  ... and {players.Count - MaxListed} more.");
        }
    }
}
