using Acorn.Infrastructure;
using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $repub - Reloads the pub data files (ECF/EIF/ENF/ESF) and refreshes the pub cache.
/// </summary>
public class RepubCommandHandler(IPubFileReloadService pubFileReload, INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["repub"];

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        await notifications.SystemMessage(playerState, "Reloading pub files...");

        var reloaded = await pubFileReload.ReloadAsync();
        var message = reloaded ? "Pub files reloaded." : "Failed to reload pub files - check the server log.";
        await notifications.SystemMessage(playerState, message);
    }
}
