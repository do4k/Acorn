using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class WarpCommandHandler : ITalkHandler
{
    private readonly WorldState _world;

    public WarpCommandHandler(WorldState world)
    {
        _world = world;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("warp", StringComparison.InvariantCultureIgnoreCase) ||
               command.Equals("w", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler, string command, params string[] args)
    {
        if (args.Length < 3)
        {
            await connectionHandler.ServerMessage("Usage: $warp <map> <x> <y>");
        }

        if (!int.TryParse(args[0], out var mapId) || !int.TryParse(args[1], out var x) ||
            !int.TryParse(args[2], out var y))
        {
            await connectionHandler.ServerMessage("Invalid coordinates.");
            return;
        }

        var map = _world.MapForId(mapId);
        if (map is null)
        {
            return;
        }
        await connectionHandler.Warp(map, x, y, WarpEffect.Admin);
    }
}