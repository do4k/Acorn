using Acorn.Net.Services;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Player;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class WarpCommandHandler : ITalkHandler
{
    private readonly INotificationService _notifications;
    private readonly IPlayerController _playerController;
    private readonly IWorldQueries _world;

    public WarpCommandHandler(IWorldQueries world, INotificationService notifications,
        IPlayerController playerController)
    {
        _world = world;
        _notifications = notifications;
        _playerController = playerController;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("warp", StringComparison.InvariantCultureIgnoreCase) ||
               command.Equals("w", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await _notifications.SystemMessage(playerState, "Usage: $warp <map id or name> [<x> <y>]");
            return;
        }

        // Try to parse the first argument as a map ID
        int? mapId = null;
        if (int.TryParse(args[0], out var parsedMapId))
        {
            mapId = parsedMapId;
        }

        // Parse optional x and y coordinates
        int? x = null;
        int? y = null;
        if (args.Length >= 3)
        {
            if (!int.TryParse(args[1], out var parsedX) || !int.TryParse(args[2], out var parsedY))
            {
                await _notifications.SystemMessage(playerState, "Invalid coordinates.");
                return;
            }

            x = parsedX;
            y = parsedY;
        }

        // Find the target map
        MapState? targetMap = null;

        if (mapId.HasValue)
        {
            // Search by ID
            targetMap = _world.FindMap(mapId.Value);
            if (targetMap is null)
            {
                await _notifications.SystemMessage(playerState, $"Map with ID {mapId} not found.");
                return;
            }
        }
        else
        {
            // Search by name
            var mapName = args[0];
            var allMaps = _world.GetAllMaps().ToList();
            var matchingMaps = allMaps
                .Where(m => m.Data.Name != null && m.Data.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingMaps.Count == 0)
            {
                await _notifications.SystemMessage(playerState, $"No maps found matching '{mapName}'.");
                return;
            }

            if (matchingMaps.Count > 1)
            {
                // Use the first match but warn about multiple matches
                targetMap = matchingMaps[0];
                var mapList = string.Join(", ", matchingMaps.Select(m => $"{m.Data.Name} ({m.Id})"));
                await _notifications.SystemMessage(playerState,
                    $"Multiple maps found: {mapList}. Warping to {targetMap.Data.Name} ({targetMap.Id}).");
            }
            else
            {
                targetMap = matchingMaps[0];
            }
        }

        // Calculate coordinates if not provided (middle of the map)
        if (!x.HasValue || !y.HasValue)
        {
            x = targetMap.Data.Width / 2;
            y = targetMap.Data.Height / 2;
        }

        await _playerController.WarpAsync(playerState, targetMap, x.Value, y.Value, WarpEffect.Admin);
    }
}