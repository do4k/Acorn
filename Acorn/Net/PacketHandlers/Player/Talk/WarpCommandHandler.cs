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

    public Task HandleAsync(PlayerConnection playerConnection, string command, params string[] args)
    {
        if (args.Length < 3)
        {
            return playerConnection.Send(new TalkServerServerPacket
            {
                Message = "Usage: $warp <map> <x> <y>"
            });
        }

        if (!int.TryParse(args[0], out var mapId) || !int.TryParse(args[1], out var x) ||
            !int.TryParse(args[2], out var y))
        {
            return playerConnection.Send(new TalkServerServerPacket
            {
                Message = "Invalid coordinates."
            });
        }

        return _world.Warp(playerConnection, mapId, x, y, WarpEffect.Admin, false);
    }
}