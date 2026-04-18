using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     #usage - Shows the player's total play time.
/// </summary>
public class UsageCommandHandler(INotificationService notifications) : IPlayerCommandHandler
{
    public bool CanHandle(string command)
        => command.Equals("usage", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var character = playerState.Character;
        if (character is null) return;

        var totalMinutes = character.Usage;
        var days = totalMinutes / 1440;
        var hours = (totalMinutes % 1440) / 60;
        var minutes = totalMinutes % 60;

        var message = $"Total play time: {days}d {hours}h {minutes}m";
        await notifications.SystemMessage(playerState, message);
    }
}
