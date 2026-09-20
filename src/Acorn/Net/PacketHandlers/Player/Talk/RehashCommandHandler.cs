using Acorn.Infrastructure;
using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $rehash - Re-reads the server configuration sources and refreshes the pub
///     data. Settings already bound at startup still require a restart.
/// </summary>
public class RehashCommandHandler(
    IConfigurationReloadService configurationReload,
    IPubFileReloadService pubFileReload,
    INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["rehash"];

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var configurationReloaded = configurationReload.Reload();
        var pubReloaded = await pubFileReload.ReloadAsync();

        var message = (configurationReloaded, pubReloaded) switch
        {
            (true, true) =>
                "Configuration reloaded and pub files refreshed. Settings bound at startup still require a restart.",
            (true, false) => "Configuration reloaded, but pub files failed to reload - check the server log.",
            (false, true) => "Pub files refreshed, but configuration failed to reload - check the server log.",
            (false, false) => "Reload failed - check the server log."
        };

        await notifications.SystemMessage(playerState, message);
    }
}
