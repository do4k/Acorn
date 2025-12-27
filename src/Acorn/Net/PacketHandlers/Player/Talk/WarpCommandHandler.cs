using Acorn.Net.Services;
using Acorn.World;
using Acorn.World.Services;
using Acorn.World.Services.Player;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class WarpCommandHandler : ITalkHandler
{
    private readonly IWorldQueries _world;
    private readonly INotificationService _notifications;
    private readonly IPlayerController _playerController;

    public WarpCommandHandler(IWorldQueries world, INotificationService notifications, IPlayerController playerController)
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
        if (args.Length < 3)
        {
            await _notifications.SystemMessage(playerState, "Usage: $warp <map> <x> <y>");
        }

        if (!int.TryParse(args[0], out var mapId) || !int.TryParse(args[1], out var x) ||
            !int.TryParse(args[2], out var y))
        {
            await _notifications.SystemMessage(playerState, "Invalid coordinates.");
            return;
        }

        var map = _world.FindMap(mapId);
        if (map is null)
        {
            return;
        }
        await _playerController.WarpAsync(playerState, map, x, y, WarpEffect.Admin);
    }
}