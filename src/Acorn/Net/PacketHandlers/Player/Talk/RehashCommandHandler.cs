using Acorn.Infrastructure;
using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $rehash - Re-reads the server configuration sources.
/// </summary>
public class RehashCommandHandler(IConfigurationReloadService configurationReload, INotificationService notifications)
    : ITalkHandler
{
    public bool CanHandle(string command)
        => command.Equals("rehash", StringComparison.InvariantCultureIgnoreCase);

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var reloaded = configurationReload.Reload();
        var message = reloaded
            ? "Configuration reloaded. Settings bound at startup still require a restart."
            : "Failed to reload configuration - check the server log.";
        await notifications.SystemMessage(playerState, message);
    }
}
