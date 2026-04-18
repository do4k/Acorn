using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     #inventory / #inv - Shows the player's inventory item count and weight.
/// </summary>
public class PlayerInventoryCommandHandler(INotificationService notifications) : IPlayerCommandHandler
{
    public bool CanHandle(string command)
        => command.Equals("inventory", StringComparison.InvariantCultureIgnoreCase)
        || command.Equals("inv", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var character = playerState.Character;
        if (character is null) return;

        var itemCount = character.Inventory.Items.Count;
        var message = $"Inventory: {itemCount} item(s)";
        await notifications.SystemMessage(playerState, message);
    }
}
