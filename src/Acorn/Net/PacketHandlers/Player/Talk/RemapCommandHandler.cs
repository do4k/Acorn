using Acorn.Database.Repository;
using Acorn.Net.Services;
using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $remap &lt;map id&gt; - Re-reads a single map file from disk and swaps it into the
///     running world. The target map must be empty: players already inside hold a live
///     reference to the old map state.
/// </summary>
public class RemapCommandHandler(
    IDataFileRepository dataFiles,
    WorldState world,
    INotificationService notifications) : ITalkHandler
{
    public IReadOnlyList<string> Commands => ["remap"];

    public string Usage => "<map id>";

    public AdminLevel RequiredLevel => AdminLevel.GameMaster;

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var mapId))
        {
            await notifications.SystemMessage(playerState, "Usage: $remap <map id>");
            return;
        }

        if (!dataFiles.TryReloadMap(mapId, out var map) || map is null)
        {
            await notifications.SystemMessage(playerState,
                $"Map {mapId}.emf was not found on disk or could not be loaded.");
            return;
        }

        if (!world.TryReplaceMap(map))
        {
            await notifications.SystemMessage(playerState,
                $"Map {mapId} is not empty - ask the players on it to leave, then run $remap again.");
            return;
        }

        await notifications.SystemMessage(playerState, $"Map {mapId} reloaded from disk.");
    }
}
